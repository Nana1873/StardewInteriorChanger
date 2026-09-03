using StardewInteriorChanger.Core;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;

namespace StardewInteriorChanger;

internal sealed class PendingClientReconcile
{
    public PendingClientReconcile(VariantId? variantId, ContentHash? contentHash)
    {
        VariantId = variantId;
        ContentHash = contentHash;
    }

    public VariantId? VariantId { get; }

    public ContentHash? ContentHash { get; }

    public int AttemptsRemaining { get; set; } = 600;
}

internal sealed record SelectionSubmission(bool IsPending, bool Success, string Message)
{
    public static SelectionSubmission Pending(string message) => new(true, false, message);

    public static SelectionSubmission Rejected(string message) => new(false, false, message);
}

public sealed class ModEntry : Mod
{
    internal const ushort MultiplayerProtocolMajor = 1;
    internal const ushort MultiplayerProtocolMinor = 0;

    private readonly Dictionary<long, PeerRegistrySnapshot> peerRegistries = new();
    private readonly Dictionary<Guid, PendingClientReconcile> pendingClientReconciles = new();
    private readonly Dictionary<Guid, string> clientReloadedMaps = new();
    private readonly Dictionary<string, bool> clientResolvedProxyModes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> reportedSelectionIssues = new(StringComparer.Ordinal);
    private readonly Dictionary<InteriorTarget, MapSnapshot> vanillaFallbackMaps = new();
    private readonly InteriorMenuRequestTracker menuRequestTracker = new();
    private long? remoteHostPlayerId;
    private IInteriorCatalog catalog = null!;
    private ModConfig config = null!;
    private WeakReference<InteriorSelectionMenu>? pendingMenu;

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<ModConfig>();
        catalog = new ContentPackInteriorCatalog(helper, Monitor, ModManifest.UniqueID);

        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.World.BuildingListChanged += OnBuildingListChanged;
        helper.Events.Multiplayer.PeerContextReceived += OnPeerContextReceived;
        helper.Events.Multiplayer.PeerConnected += OnPeerConnected;
        helper.Events.Multiplayer.PeerDisconnected += OnPeerDisconnected;
        helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
        helper.Events.Input.ButtonsChanged += OnButtonsChanged;

        helper.ConsoleCommands.Add(
            "sic",
            "Interior Changer commands: sic targets | sic list | sic current [buildingId] | " +
            "sic set <variantId> [buildingId] | sic vanilla [buildingId] | " +
            "sic menu [buildingId]",
            OnConsoleCommand);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        catalog.Reload();
        LoadVanillaFallbackMaps();
        PreflightManagedMapAssets();
    }

    private void PreflightManagedMapAssets()
    {
        int failures = 0;
        foreach (RuntimeInterior interior in catalog.Entries)
        {
            try
            {
                Helper.GameContent.InvalidateCache(interior.MapAssetKey);
                xTile.Map resolved = Helper.GameContent.Load<xTile.Map>(interior.MapAssetKey);
                if (!MapContractValidator.TryValidate(
                        interior.Definition.Target,
                        resolved,
                        out string reason))
                {
                    throw new InvalidOperationException(reason);
                }
            }
            catch (Exception exception)
            {
                failures++;
                Monitor.Log(
                    $"Managed map preflight failed for '{interior.Definition.Id}'. " +
                    $"The variant can't be applied safely. {exception}",
                    LogLevel.Error);
            }
            finally
            {
                Helper.GameContent.InvalidateCache(interior.MapAssetKey);
            }
        }

        if (failures == 0)
        {
            Monitor.Log(
                $"Preflighted {catalog.Entries.Count} managed interior map(s) and " +
                $"prepared {vanillaFallbackMaps.Count} safe Vanilla fallback map(s).",
                LogLevel.Info);
        }
    }

    private void LoadVanillaFallbackMaps()
    {
        vanillaFallbackMaps.Clear();
        var assets = new Dictionary<InteriorTarget, string>
        {
            [InteriorTarget.Greenhouse] = "Maps/Greenhouse",
            [InteriorTarget.DeluxeBarn] = "Maps/Barn3",
        };

        foreach ((InteriorTarget target, string assetName) in assets)
        {
            try
            {
                xTile.Map map = Helper.GameContent.Load<xTile.Map>(assetName);
                if (!MapContractValidator.TryValidate(target, map, out string reason))
                {
                    throw new InvalidOperationException(
                        $"Vanilla map '{assetName}' violates the {target} contract: {reason}");
                }

                vanillaFallbackMaps[target] = MapSnapshot.Capture(map);
            }
            catch (Exception exception)
            {
                Monitor.Log(
                    $"Couldn't prepare the safe {target} fallback map. {exception}",
                    LogLevel.Error);
            }
        }
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        IMultiplayerPeer? hostPeer = Helper.Multiplayer.GetConnectedPlayers()
            .FirstOrDefault(peer => peer.IsHost);
        if (hostPeer is not null)
        {
            remoteHostPlayerId = hostPeer.PlayerID;
        }

        HashSet<long> connected = Helper.Multiplayer.GetConnectedPlayers()
            .Select(peer => peer.PlayerID)
            .ToHashSet();
        foreach (long stalePeer in peerRegistries.Keys.Where(id => !connected.Contains(id)).ToArray())
        {
            peerRegistries.Remove(stalePeer);
        }
        pendingClientReconciles.Clear();
        ResetClientManagedMapState();
        reportedSelectionIssues.Clear();

        ReconcileAllBuildings(allowPopulatedExactRestore: true);
        foreach (IMultiplayerPeer peer in Helper.Multiplayer.GetConnectedPlayers())
        {
            SendRegistryHello(peer.PlayerID);
        }
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        string assetName = e.NameWithoutLocale.Name;
        if (!catalog.TryGetManagedMapTarget(assetName, out InteriorTarget target))
        {
            return;
        }

        MapSnapshot? selected = null;
        bool customSelected = false;
        if (catalog.TryGetByMapAssetKey(assetName, out RuntimeInterior interior)
            && CanLoadCustomMapOnThisPeer(interior))
        {
            selected = interior.Map;
            customSelected = true;
        }
        else
        {
            vanillaFallbackMaps.TryGetValue(target, out selected);
        }

        if (selected is null)
        {
            Monitor.Log(
                $"No safe {target} fallback is available for managed map '{assetName}'.",
                LogLevel.Error);
            return;
        }

        MapSnapshot snapshot = selected;
        string normalizedAssetName = NormalizeAssetName(assetName);
        bool trackClientResolution = !Context.IsOnHostComputer && remoteHostPlayerId is not null;
        e.LoadFrom(
            () =>
            {
                xTile.Map map = snapshot.CreateMap();
                if (trackClientResolution)
                {
                    clientResolvedProxyModes[normalizedAssetName] = customSelected;
                }

                return map;
            },
            AssetLoadPriority.Exclusive);
    }

    private void OnPeerContextReceived(object? sender, PeerContextReceivedEventArgs e)
    {
        if (e.Peer.IsHost)
        {
            remoteHostPlayerId = e.Peer.PlayerID;
            ResetClientManagedMapState();
        }

        if (e.Peer.GetMod(ModManifest.UniqueID) is null)
        {
            Monitor.Log(
                $"Player {e.Peer.PlayerID} doesn't have {ModManifest.UniqueID}. " +
                "A save with active custom interiors requires the Core on every peer.",
                LogLevel.Error);
            return;
        }

        SendRegistryHello(e.Peer.PlayerID);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e) =>
        ReconcileAllBuildings(allowPopulatedExactRestore: false);

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        peerRegistries.Clear();
        pendingClientReconciles.Clear();
        clientReloadedMaps.Clear();
        clientResolvedProxyModes.Clear();
        reportedSelectionIssues.Clear();
        menuRequestTracker.Reset();
        pendingMenu = null;
        remoteHostPlayerId = null;
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (config.OpenMenu.JustPressed())
        {
            TryOpenMenu(null);
        }
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || Context.IsOnHostComputer)
        {
            return;
        }

        if (pendingClientReconciles.Count > 0)
        {
            ProcessPendingClientReconciles();
        }

        GuardClientInteriorAccess();
        if (e.Ticks % 30 == 0)
        {
            ScanClientSynchronizedMaps();
        }
    }

    private void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
    {
        if (Context.IsWorldReady)
        {
            ReconcileAllBuildings(allowPopulatedExactRestore: false);
        }
    }

    private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
    {
        if (e.Peer.IsHost)
        {
            remoteHostPlayerId = e.Peer.PlayerID;
            ResetClientManagedMapState();
        }

        if (e.Peer.GetMod(ModManifest.UniqueID) is null)
        {
            Monitor.Log(
                $"Player {e.Peer.PlayerID} doesn't have {ModManifest.UniqueID}. " +
                "Interior changes will remain locked while that peer is connected.",
                LogLevel.Error);
            return;
        }

        SendRegistryHello(e.Peer.PlayerID);
    }

    private void OnPeerDisconnected(object? sender, PeerDisconnectedEventArgs e)
    {
        peerRegistries.Remove(e.Peer.PlayerID);
        if (remoteHostPlayerId == e.Peer.PlayerID)
        {
            remoteHostPlayerId = null;
            ResetClientManagedMapState();
        }
    }

    private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (!string.Equals(e.FromModID, ModManifest.UniqueID, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            switch (e.Type)
            {
                case MultiplayerMessageTypes.RegistryHello:
                    ReceiveRegistryHello(e.FromPlayerID, e.ReadAs<RegistryHelloMessage>());
                    break;
                case MultiplayerMessageTypes.SelectionRequest when Context.IsMainPlayer:
                    ReceiveSelectionRequest(e.FromPlayerID, e.ReadAs<SelectionRequestMessage>());
                    break;
                case MultiplayerMessageTypes.SelectionResult when !Context.IsMainPlayer:
                    ReceiveSelectionResult(e.ReadAs<SelectionResultMessage>());
                    break;
                case MultiplayerMessageTypes.SelectionCommitted when !Context.IsMainPlayer:
                    ReceiveSelectionCommitted(
                        e.FromPlayerID,
                        e.ReadAs<SelectionCommittedMessage>());
                    break;
            }
        }
        catch (Exception exception)
        {
            Monitor.Log(
                $"Ignored malformed multiplayer message '{e.Type}' from player " +
                $"{e.FromPlayerID}. {exception}",
                LogLevel.Warn);
        }
    }

    private void SendRegistryHello(long playerId)
    {
        Helper.Multiplayer.SendMessage(
            new RegistryHelloMessage
            {
                ProtocolMajor = MultiplayerProtocolMajor,
                ProtocolMinor = MultiplayerProtocolMinor,
                ModVersion = ModManifest.Version.ToString(),
                Variants = catalog.Fingerprints.Select(fingerprint =>
                    new VariantFingerprintMessage
                    {
                        Id = fingerprint.Id.Value,
                        Target = fingerprint.Target.ToString(),
                        ContentHash = fingerprint.ContentHash.Value,
                    }).ToList(),
            },
            MultiplayerMessageTypes.RegistryHello,
            modIDs: new[] { ModManifest.UniqueID },
            playerIDs: new[] { playerId });
    }

    private void ReceiveRegistryHello(long playerId, RegistryHelloMessage message)
    {
        var variants = new List<VariantFingerprint>();
        foreach (VariantFingerprintMessage raw in message.Variants ?? new())
        {
            if (!VariantId.TryParse(raw.Id, out VariantId id)
                || !Enum.TryParse(raw.Target, ignoreCase: false, out InteriorTarget target)
                || !Enum.IsDefined(typeof(InteriorTarget), target)
                || !ContentHash.TryParse(raw.ContentHash, out ContentHash hash))
            {
                Monitor.Log(
                    $"Ignored invalid registry hello from player {playerId}.",
                    LogLevel.Error);
                peerRegistries.Remove(playerId);
                if (!Context.IsMainPlayer && remoteHostPlayerId == playerId)
                {
                    ResetClientManagedMapState();
                }
                return;
            }

            variants.Add(new VariantFingerprint(id, target, hash));
        }

        peerRegistries[playerId] = new PeerRegistrySnapshot(
            playerId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            message.ProtocolMajor,
            message.ProtocolMinor,
            variants);

        if (!Context.IsMainPlayer && remoteHostPlayerId == playerId)
        {
            ResetClientManagedMapState();
        }

        if (Context.IsMainPlayer && !TryValidatePeerParity(
                changingBuilding: null,
                requestedVariant: null,
                out string reason))
        {
            Monitor.Log($"Multiplayer interior parity isn't ready: {reason}", LogLevel.Error);
        }
        else
        {
            Monitor.Log(
                $"Received interior registry from player {playerId} " +
                $"({variants.Count} variant(s)).",
                LogLevel.Trace);
        }
    }

    private void ReceiveSelectionRequest(long playerId, SelectionRequestMessage request)
    {
        if (Helper.Multiplayer.GetConnectedPlayer(playerId) is null)
        {
            return;
        }

        if (!Guid.TryParse(request.BuildingId, out Guid buildingId)
            || FindBuilding(buildingId) is not { } building)
        {
            SendSelectionResult(playerId, request, false, "The requested building doesn't exist.");
            return;
        }

        bool success = TrySetSelection(building, request.VariantId, out string message);
        SendSelectionResult(playerId, request, success, message);
    }

    private void SendSelectionResult(
        long playerId,
        SelectionRequestMessage request,
        bool success,
        string message)
    {
        Helper.Multiplayer.SendMessage(
            new SelectionResultMessage
            {
                Success = success,
                BuildingId = request.BuildingId,
                VariantId = request.VariantId,
                Message = message,
            },
            MultiplayerMessageTypes.SelectionResult,
            modIDs: new[] { ModManifest.UniqueID },
            playerIDs: new[] { playerId });
    }

    private void ReceiveSelectionResult(SelectionResultMessage result)
    {
        Monitor.Log(result.Message, result.Success ? LogLevel.Info : LogLevel.Error);
        if (!menuRequestTracker.TryComplete(
                result.BuildingId,
                result.VariantId,
                out _))
        {
            return;
        }

        InteriorSelectionMenu? menu = null;
        pendingMenu?.TryGetTarget(out menu);
        pendingMenu = null;
        if (menu is not null && ReferenceEquals(Game1.activeClickableMenu, menu))
        {
            menu.HandleSelectionResult(
                result.Success,
                result.BuildingId,
                result.VariantId,
                result.Message);
            return;
        }

        Game1.addHUDMessage(new HUDMessage(
            result.Message,
            result.Success ? HUDMessage.newQuest_type : HUDMessage.error_type));
    }

    private void ReceiveSelectionCommitted(
        long playerId,
        SelectionCommittedMessage message)
    {
        if (!Context.IsWorldReady
            || playerId != Game1.MasterPlayer.UniqueMultiplayerID
            || !Guid.TryParse(message.BuildingId, out Guid buildingId))
        {
            return;
        }

        if (message.VariantId is null)
        {
            pendingClientReconciles[buildingId] = new PendingClientReconcile(null, null);
            return;
        }

        if (!VariantId.TryParse(message.VariantId, out VariantId variantId)
            || !ContentHash.TryParse(message.ContentHash, out ContentHash contentHash)
            || !catalog.TryGet(variantId, out RuntimeInterior local)
            || local.Definition.ContentHash != contentHash)
        {
            Monitor.Log(
                $"Host committed '{message.VariantId}', but this client doesn't have " +
                "the exact same gameplay content. The map wasn't reloaded.",
                LogLevel.Error);
            return;
        }

        pendingClientReconciles[buildingId] =
            new PendingClientReconcile(variantId, contentHash);
    }

    private void ProcessPendingClientReconciles()
    {
        foreach ((Guid buildingId, PendingClientReconcile pending) in
                 pendingClientReconciles.ToArray())
        {
            Building? building = FindBuilding(buildingId);
            InteriorTarget? target = building is null ? null : Classify(building);
            GameLocation? indoors = building?.GetIndoors();
            if (building is not null && target is not null && indoors is not null
                && TryGetExpectedMapFromCommit(building, target.Value, pending, out string expectedMap)
                && AssetNamesEqual(indoors.mapPath.Value, expectedMap)
                && !HasOnlinePlayerInside(indoors))
            {
                try
                {
                    ReloadSynchronizedMap(building, indoors, target.Value);
                    clientReloadedMaps[buildingId] = GetClientReloadStateKey(
                        expectedMap,
                        WasClientMapResolvedAsCustom(expectedMap));
                    pendingClientReconciles.Remove(buildingId);
                    continue;
                }
                catch (Exception exception)
                {
                    Monitor.Log(
                        $"Client couldn't reload synchronized interior for {buildingId}. " +
                        $"{exception}",
                        LogLevel.Error);
                    pendingClientReconciles.Remove(buildingId);
                    continue;
                }
            }

            pending.AttemptsRemaining--;
            if (pending.AttemptsRemaining <= 0)
            {
                Monitor.Log(
                    $"Timed out waiting for synchronized interior state for {buildingId}.",
                    LogLevel.Error);
                pendingClientReconciles.Remove(buildingId);
            }
        }
    }

    private bool TryGetExpectedMapFromCommit(
        Building building,
        InteriorTarget target,
        PendingClientReconcile pending,
        out string mapAssetKey)
    {
        mapAssetKey = string.Empty;
        SelectionReadResult stored = SelectionStorage.Read(building, target);
        if (!stored.IsValid)
        {
            return false;
        }

        if (pending.VariantId is null)
        {
            return stored.Selection.Choice is InteriorChoice.VanillaChoice
                && TryResolveVanillaMap(building, out mapAssetKey);
        }

        if (stored.Selection.Choice is InteriorChoice.CustomChoice custom
            && custom.VariantId == pending.VariantId.Value
            && custom.ContentHash == pending.ContentHash
            && catalog.TryGet(custom.VariantId, out RuntimeInterior interior)
            && interior.Definition.Target == target
            && interior.Definition.TargetContract == stored.Selection.TargetContract
            && CanLoadCustomMapOnThisPeer(interior))
        {
            mapAssetKey = GetManagedMapAssetKey(interior, building);
            return true;
        }

        return false;
    }

    private void ScanClientSynchronizedMaps()
    {
        foreach (Building building in GetFarmBuildings())
        {
            if (Classify(building) is not { } target || building.GetIndoors() is not { } indoors)
            {
                continue;
            }

            SelectionReadResult stored = SelectionStorage.Read(building, target);
            if (!stored.IsValid || !stored.IsExplicit
                || !TryGetClientExpectedMap(
                    stored.Selection,
                    building,
                    indoors,
                    target,
                    out string expectedMap,
                    out bool customAuthorized)
                || !AssetNamesEqual(indoors.mapPath.Value, expectedMap)
                || HasOnlinePlayerInside(indoors))
            {
                continue;
            }

            Guid buildingId = building.id.Value;
            string stateKey = GetClientReloadStateKey(expectedMap, customAuthorized);
            string observedStateKey = GetClientReloadStateKey(
                expectedMap,
                WasClientMapResolvedAsCustom(expectedMap));
            if (clientReloadedMaps.TryGetValue(buildingId, out string? loaded)
                && string.Equals(loaded, stateKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(loaded, observedStateKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                ReloadSynchronizedMap(building, indoors, target);
                clientReloadedMaps[buildingId] = GetClientReloadStateKey(
                    expectedMap,
                    WasClientMapResolvedAsCustom(expectedMap));
            }
            catch (Exception exception)
            {
                Monitor.Log(
                    $"Client couldn't reconcile synchronized interior for {buildingId:D}. " +
                    $"{exception}",
                    LogLevel.Error);
            }
        }
    }

    private bool TryGetClientExpectedMap(
        StoredSelection stored,
        Building building,
        GameLocation indoors,
        InteriorTarget target,
        out string mapAssetKey,
        out bool customAuthorized)
    {
        customAuthorized = false;
        if (stored.Choice is InteriorChoice.VanillaChoice)
        {
            return TryResolveVanillaMap(building, out mapAssetKey);
        }

        var custom = (InteriorChoice.CustomChoice)stored.Choice;
        if (catalog.TryGet(custom.VariantId, out RuntimeInterior interior)
            && interior.Definition.Target == target
            && interior.Definition.TargetContract == stored.TargetContract
            && interior.Definition.ContentHash == custom.ContentHash)
        {
            mapAssetKey = GetManagedMapAssetKey(interior, building);
            customAuthorized = CanLoadCustomMapOnThisPeer(interior);
            return true;
        }

        if (catalog.TryGetManagedMapTarget(indoors.mapPath.Value, out InteriorTarget managedTarget)
            && managedTarget == target)
        {
            mapAssetKey = indoors.mapPath.Value;
            return true;
        }

        mapAssetKey = string.Empty;
        return false;
    }

    private bool CanLoadCustomMapOnThisPeer(RuntimeInterior interior)
    {
        if (Context.IsOnHostComputer
            || (!Context.IsWorldReady && remoteHostPlayerId is null))
        {
            return true;
        }

        if (remoteHostPlayerId is not { } hostId
            || !peerRegistries.TryGetValue(hostId, out PeerRegistrySnapshot? host)
            || host.ProtocolMajor != MultiplayerProtocolMajor)
        {
            return false;
        }

        VariantFingerprint expected = interior.Fingerprint;
        return host.Variants.Count(candidate => candidate == expected) == 1;
    }

    private void ResetClientManagedMapState()
    {
        clientReloadedMaps.Clear();
        clientResolvedProxyModes.Clear();
    }

    private void GuardClientInteriorAccess()
    {
        Building? building = FindCurrentBuilding();
        if (building is null
            || Classify(building) is not { } target
            || building.GetIndoors() is not { } indoors
            || !catalog.TryGetManagedMapTarget(indoors.mapPath.Value, out InteriorTarget managedTarget)
            || managedTarget != target)
        {
            return;
        }

        SelectionReadResult stored = SelectionStorage.Read(building, target);
        InteriorChoice.CustomChoice? custom = stored.IsValid
            ? stored.Selection.Choice as InteriorChoice.CustomChoice
            : null;
        if (custom is not null
            && catalog.TryGet(custom.VariantId, out RuntimeInterior interior)
            && interior.Definition.Target == target
            && interior.Definition.TargetContract == stored.Selection.TargetContract
            && interior.Definition.ContentHash == custom.ContentHash
            && CanLoadCustomMapOnThisPeer(interior)
            && AssetNamesEqual(indoors.mapPath.Value, GetManagedMapAssetKey(interior, building))
            && clientReloadedMaps.TryGetValue(building.id.Value, out string? loadedState)
            && string.Equals(
                loadedState,
                GetClientReloadStateKey(GetManagedMapAssetKey(interior, building), true),
                StringComparison.OrdinalIgnoreCase)
            && WasClientMapResolvedAsCustom(indoors.mapPath.Value))
        {
            return;
        }

        var outside = building.getPointForHumanDoor();
        string choice = custom is null ? "the synchronized custom interior" : $"'{custom.VariantId}'";
        ReportSelectionIssueOnce(
            building,
            $"Farmhand access to {choice} was blocked because the host " +
            "handshake or exact gameplay content doesn't match. Returning to the Farm.");
        Game1.warpFarmer("Farm", outside.X, outside.Y + 1, false);
    }

    private void OnConsoleCommand(string command, string[] args)
    {
        string action = args.FirstOrDefault()?.ToLowerInvariant() ?? string.Empty;
        switch (action)
        {
            case "targets":
                ListTargets();
                break;
            case "list":
                ListVariants();
                break;
            case "current":
                ShowCurrentSelection(args.ElementAtOrDefault(1));
                break;
            case "set" when args.Length >= 2:
                RequestSelection(args[1], args.ElementAtOrDefault(2));
                break;
            case "vanilla":
                RequestSelection(null, args.ElementAtOrDefault(1));
                break;
            case "menu":
                TryOpenMenu(args.ElementAtOrDefault(1));
                break;
            default:
                Monitor.Log(
                    "Usage: sic targets | sic list | sic current [buildingId] | " +
                    "sic set <variantId> [buildingId] | sic vanilla [buildingId] | " +
                    "sic menu [buildingId]",
                    LogLevel.Info);
                break;
        }
    }

    private void ListTargets()
    {
        if (!RequireWorldReady())
        {
            return;
        }

        string[] targets = GetFarmBuildings()
            .Select(building => new { Building = building, Target = Classify(building) })
            .Where(item => item.Target is not null)
            .Select(item =>
                $"  {item.Building.id.Value:D} [{item.Target}] " +
                $"map={item.Building.GetIndoors()?.mapPath.Value ?? "<none>"}")
            .ToArray();

        Monitor.Log(
            targets.Length == 0
                ? "No supported Greenhouse or Deluxe Barn exists in this save."
                : "Supported interior targets:\n" + string.Join("\n", targets),
            LogLevel.Info);
    }

    private void ListVariants()
    {
        InteriorTarget? currentTarget = FindCurrentBuilding() is { } building
            ? Classify(building)
            : null;
        RuntimeInterior[] entries = catalog.Entries
            .Where(entry => currentTarget is null
                || entry.Definition.Target == currentTarget.Value)
            .ToArray();

        Monitor.Log(
            entries.Length == 0
                ? "No valid interior variants are installed."
                : "Available interior variants:\n" + string.Join(
                    "\n",
                    entries.Select(entry =>
                        $"  {entry.Definition.Id.Value} — {entry.Definition.DisplayName} " +
                        $"[{entry.Definition.Target}] hash={ShortHash(entry.Definition.ContentHash)}")),
            LogLevel.Info);
    }

    private void ShowCurrentSelection(string? buildingToken)
    {
        if (!TryResolveCommandBuilding(buildingToken, out Building building, out InteriorTarget target))
        {
            return;
        }

        SelectionReadResult stored = SelectionStorage.Read(building, target);
        string choice = stored.IsValid
            ? FormatChoice(stored.Selection.Choice)
            : $"invalid ({stored.Error})";
        Monitor.Log(
            $"Building {building.id.Value:D} [{target}] requests {choice}; " +
            $"loaded map is '{building.GetIndoors()?.mapPath.Value ?? "<none>"}'.",
            stored.IsValid ? LogLevel.Info : LogLevel.Error);
    }

    private void RequestSelection(string? variantValue, string? buildingToken)
    {
        if (!TryResolveCommandBuilding(buildingToken, out Building building, out InteriorTarget target))
        {
            return;
        }

        SelectionSubmission result = SubmitSelection(building, target, variantValue);
        Monitor.Log(
            result.Message,
            result.IsPending || result.Success ? LogLevel.Info : LogLevel.Error);
    }

    internal void RequestMenuSelection(
        InteriorSelectionMenu menu,
        Building building,
        InteriorTarget target,
        string? variantValue)
    {
        string buildingId = building.id.Value.ToString("D");
        InteriorMenuRequest? request = null;
        if (!Context.IsMainPlayer)
        {
            if (!menuRequestTracker.TryBegin(buildingId, variantValue, out request))
            {
                menu.ShowPendingBlocked();
                return;
            }

            pendingMenu = new WeakReference<InteriorSelectionMenu>(menu);
        }

        SelectionSubmission result = SubmitSelection(building, target, variantValue);
        Monitor.Log(
            result.Message,
            result.IsPending || result.Success ? LogLevel.Info : LogLevel.Error);
        if (result.IsPending)
        {
            menu.ShowPending();
            return;
        }

        if (request is not null)
        {
            menuRequestTracker.TryCancel(request);
            pendingMenu = null;
        }

        menu.HandleSelectionResult(result.Success, buildingId, variantValue, result.Message);
    }

    private SelectionSubmission SubmitSelection(
        Building building,
        InteriorTarget target,
        string? variantValue)
    {
        string? canonicalVariant = null;
        if (variantValue is not null)
        {
            if (!catalog.TryGet(variantValue, out RuntimeInterior interior))
            {
                return SelectionSubmission.Rejected(
                    $"Unknown interior variant '{variantValue}'. Run 'sic list' for valid IDs.");
            }

            if (interior.Definition.Target != target)
            {
                return SelectionSubmission.Rejected(
                    $"Variant '{interior.Definition.Id}' targets " +
                    $"{interior.Definition.Target}, not {target}.");
            }

            canonicalVariant = interior.Definition.Id.Value;
        }

        if (Context.IsMainPlayer)
        {
            bool success = TrySetSelection(building, canonicalVariant, out string result);
            return new SelectionSubmission(false, success, result);
        }

        long hostId = Game1.MasterPlayer.UniqueMultiplayerID;
        if (canonicalVariant is not null)
        {
            if (!peerRegistries.TryGetValue(hostId, out PeerRegistrySnapshot? host)
                || host.ProtocolMajor != MultiplayerProtocolMajor)
            {
                return SelectionSubmission.Rejected(
                    "The host's Interior Changer handshake is missing or incompatible.");
            }

            if (!ClientAndHostShareVariant(host, canonicalVariant, out string mismatch))
            {
                return SelectionSubmission.Rejected(mismatch);
            }
        }

        Helper.Multiplayer.SendMessage(
            new SelectionRequestMessage
            {
                BuildingId = building.id.Value.ToString("D"),
                VariantId = canonicalVariant,
            },
            MultiplayerMessageTypes.SelectionRequest,
            modIDs: new[] { ModManifest.UniqueID },
            playerIDs: new[] { hostId });
        return SelectionSubmission.Pending("Interior change request sent to the host.");
    }

    private void TryOpenMenu(string? buildingToken)
    {
        if (!Context.IsWorldReady)
        {
            Monitor.Log(Helper.Translation.Get("menu.open.not-ready"), LogLevel.Error);
            return;
        }

        if (!Context.IsPlayerFree
            || Game1.activeClickableMenu is not null
            || Game1.currentMinigame is not null
            || Game1.eventUp
            || !Game1.player.CanMove)
        {
            Monitor.Log(Helper.Translation.Get("menu.open.unsafe"), LogLevel.Error);
            return;
        }

        IReadOnlyList<(Building Building, InteriorTarget Target)> buildings =
            GetSupportedMenuBuildings();
        if (buildings.Count == 0)
        {
            Monitor.Log(Helper.Translation.Get("menu.empty"), LogLevel.Error);
            return;
        }

        Guid? selectedId = FindCurrentBuilding()?.id.Value;
        if (!string.IsNullOrWhiteSpace(buildingToken))
        {
            if (!TryResolveCommandBuilding(
                    buildingToken,
                    out Building requested,
                    out _))
            {
                return;
            }

            selectedId = requested.id.Value;
        }

        var menu = new InteriorSelectionMenu(
            this,
            Helper,
            Monitor,
            catalog,
            buildings,
            selectedId);
        if (menuRequestTracker.Pending is not null)
        {
            pendingMenu = new WeakReference<InteriorSelectionMenu>(menu);
            menu.ShowPending();
        }

        Game1.activeClickableMenu = menu;
    }

    private static IReadOnlyList<(Building Building, InteriorTarget Target)>
        GetSupportedMenuBuildings() => GetFarmBuildings()
            .Select(building => (Building: building, Target: Classify(building)))
            .Where(item => item.Target is not null && item.Building.GetIndoors() is not null)
            .Select(item => (Building: item.Building, Target: item.Target!.Value))
            .OrderBy(item => item.Target == InteriorTarget.Greenhouse ? 0 : 1)
            .ThenBy(item => item.Building.tileY.Value)
            .ThenBy(item => item.Building.tileX.Value)
            .ThenBy(item => item.Building.id.Value)
            .ToArray();

    private bool ClientAndHostShareVariant(
        PeerRegistrySnapshot host,
        string variantValue,
        out string message)
    {
        VariantId id = VariantId.Parse(variantValue);
        PeerCompatibilityResult compatibility = PeerCompatibilityEvaluator.Evaluate(
            MultiplayerProtocolMajor,
            MultiplayerProtocolMinor,
            catalog.Fingerprints,
            new[] { id },
            new[] { host });
        if (compatibility.IsCompatible)
        {
            message = string.Empty;
            return true;
        }

        message = "The host doesn't have the exact same gameplay content for this variant: " +
            string.Join("; ", compatibility.Issues.Select(issue => issue.Message));
        return false;
    }

    private bool TrySetSelection(
        Building building,
        string? variantValue,
        out string message)
    {
        if (!Context.IsMainPlayer)
        {
            message = "Only the host may change shared building state.";
            return false;
        }

        InteriorTarget? target = Classify(building);
        GameLocation? indoors = building.GetIndoors();
        if (target is null || indoors is null)
        {
            message = "Only loaded Greenhouses and Deluxe Barns are supported.";
            return false;
        }

        TargetContractId contract = TargetContracts.For(target.Value);
        StoredSelection requested;
        string nextMap;
        VariantId? requestedVariant = null;
        if (variantValue is null)
        {
            if (!TryResolveVanillaMap(building, out nextMap))
            {
                message = "The building data doesn't define a vanilla interior map.";
                return false;
            }

            requested = StoredSelection.Create(
                SelectionStorage.GetInstanceId(building, target.Value),
                contract,
                InteriorChoice.Vanilla);
        }
        else
        {
            if (!catalog.TryGet(variantValue, out RuntimeInterior interior)
                || interior.Definition.Target != target.Value
                || interior.Definition.TargetContract != contract)
            {
                message = $"Variant '{variantValue}' isn't compatible with this building.";
                return false;
            }

            requestedVariant = interior.Definition.Id;
            nextMap = GetManagedMapAssetKey(interior, building);
            requested = StoredSelection.Create(
                SelectionStorage.GetInstanceId(building, target.Value),
                contract,
                InteriorChoice.Custom(
                    interior.Definition.Id,
                    interior.Definition.ContentHash));
        }

        if (requestedVariant is not null
            && !TryValidatePeerParity(building, requestedVariant, out message))
        {
            return false;
        }

        bool sameMap = AssetNamesEqual(indoors.mapPath.Value, nextMap);
        if (!sameMap)
        {
            SwitchSafetyResult safety = SwitchSafetyInspector.Inspect(
                building,
                indoors,
                target.Value);
            if (!safety.IsSafe)
            {
                message = "Interior change blocked: " + safety.ToUserMessage() +
                    ". Clear the room, make sure every player has left, then retry. " +
                    "Use 'sic targets' outside to address the building by ID.";
                return false;
            }
        }

        SelectionDataSnapshot previousSelection =
            SelectionStorage.Capture(building, contract);
        if (!TryApplyMapAndStateAtomically(
                building,
                indoors,
                target.Value,
                nextMap,
                commitState: () => SelectionStorage.Write(building, requested),
                rollbackState: () => SelectionStorage.Restore(building, previousSelection),
                out Exception? applyFailure,
                out Exception? rollbackFailure))
        {
            if (rollbackFailure is not null)
            {
                Monitor.Log(
                    $"Interior rollback also failed for building {building.id.Value:D}. " +
                    $"{rollbackFailure}",
                    LogLevel.Error);
            }

            Monitor.Log(
                $"Failed to apply interior '{variantValue ?? "vanilla"}' to building " +
                $"{building.id.Value:D}. {applyFailure}",
                LogLevel.Error);
            message = rollbackFailure is null
                ? "The interior map couldn't be applied; the previous state was restored."
                : "The interior map couldn't be applied and rollback was incomplete; see the log.";
            return false;
        }

        BroadcastCommittedSelection(building, requested.Choice);

        message = requested.Choice is InteriorChoice.CustomChoice custom
            ? $"Building {building.id.Value:D} now uses '{custom.VariantId.Value}'."
            : $"Building {building.id.Value:D} now uses its vanilla interior.";
        return true;
    }

    private bool TryValidatePeerParity(
        Building? changingBuilding,
        VariantId? requestedVariant,
        out string message)
    {
        IMultiplayerPeer[] peers = Helper.Multiplayer.GetConnectedPlayers().ToArray();
        if (peers.Length == 0)
        {
            message = string.Empty;
            return true;
        }

        var snapshots = new List<PeerRegistrySnapshot>();
        foreach (IMultiplayerPeer peer in peers)
        {
            if (peer.GetMod(ModManifest.UniqueID) is null)
            {
                message = $"player {peer.PlayerID} doesn't have the Core mod";
                return false;
            }

            if (!peerRegistries.TryGetValue(peer.PlayerID, out PeerRegistrySnapshot? snapshot))
            {
                message = $"still waiting for player {peer.PlayerID}'s registry handshake";
                return false;
            }

            snapshots.Add(snapshot);
        }

        HashSet<VariantId> required = GetActiveCustomVariants(changingBuilding);
        if (requestedVariant is not null)
        {
            required.Add(requestedVariant.Value);
        }

        PeerCompatibilityResult compatibility = PeerCompatibilityEvaluator.Evaluate(
            MultiplayerProtocolMajor,
            MultiplayerProtocolMinor,
            catalog.Fingerprints,
            required,
            snapshots);
        if (compatibility.IsCompatible)
        {
            message = string.Empty;
            return true;
        }

        message = string.Join(
            "; ",
            compatibility.Issues.Select(issue =>
                issue.VariantId is null
                    ? $"peer {issue.PeerId ?? "host"}: {issue.Message}"
                    : $"peer {issue.PeerId ?? "host"}, {issue.VariantId}: {issue.Message}"));
        return false;
    }

    private HashSet<VariantId> GetActiveCustomVariants(Building? excluded)
    {
        var result = new HashSet<VariantId>();
        foreach (Building building in GetFarmBuildings())
        {
            if (ReferenceEquals(building, excluded) || Classify(building) is not { } target)
            {
                continue;
            }

            SelectionReadResult stored = SelectionStorage.Read(building, target);
            if (stored.IsValid
                && stored.Selection.Choice is InteriorChoice.CustomChoice custom)
            {
                result.Add(custom.VariantId);
            }
        }

        return result;
    }

    private void ReconcileAllBuildings(bool allowPopulatedExactRestore)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        foreach (Building building in GetFarmBuildings())
        {
            if (Classify(building) is { } target)
            {
                ReconcileBuilding(building, target, allowPopulatedExactRestore);
            }
        }
    }

    private void ReconcileBuilding(
        Building building,
        InteriorTarget target,
        bool allowPopulatedExactRestore)
    {
        GameLocation? indoors = building.GetIndoors();
        if (indoors is null)
        {
            return;
        }

        SelectionReadResult stored = SelectionStorage.Read(building, target);
        if (!stored.IsValid)
        {
            ReportSelectionIssueOnce(
                building,
                $"Saved interior selection is invalid: {stored.Error}. It was left unchanged.");
            return;
        }

        if (stored.Selection.Choice is InteriorChoice.VanillaChoice)
        {
            if (Context.IsMainPlayer)
            {
                SelectionStorage.ClearRequiresEmptyRestore(
                    building,
                    stored.Selection.TargetContract);
            }

            if (!stored.IsExplicit || !TryResolveVanillaMap(building, out string vanillaMap)
                || AssetNamesEqual(indoors.mapPath.Value, vanillaMap))
            {
                return;
            }

            if (!Context.IsMainPlayer)
            {
                return;
            }

            SwitchSafetyResult safety = SwitchSafetyInspector.Inspect(building, indoors, target);
            if (!safety.IsSafe)
            {
                ReportSelectionIssueOnce(
                    building,
                    "Vanilla is selected, but restoring its map is unsafe: " +
                    safety.ToUserMessage());
                return;
            }

            ApplyAndBroadcastTrusted(
                building,
                indoors,
                target,
                vanillaMap,
                stored.Selection.Choice);
            return;
        }

        var custom = (InteriorChoice.CustomChoice)stored.Selection.Choice;
        if (!catalog.TryGet(custom.VariantId, out RuntimeInterior interior)
            || interior.Definition.Target != target
            || interior.Definition.TargetContract != stored.Selection.TargetContract)
        {
            if (Context.IsMainPlayer)
            {
                SelectionStorage.MarkRequiresEmptyRestore(
                    building,
                    stored.Selection.TargetContract);
            }

            ReportSelectionIssueOnce(
                building,
                $"Requested variant '{custom.VariantId}' is unavailable or targets a " +
                "different building. The saved request was preserved.");
            return;
        }

        if (interior.Definition.ContentHash != custom.ContentHash)
        {
            if (Context.IsMainPlayer)
            {
                SelectionStorage.MarkRequiresEmptyRestore(
                    building,
                    stored.Selection.TargetContract);
            }

            ReportSelectionIssueOnce(
                building,
                $"Variant '{custom.VariantId}' changed gameplay content. Re-select it " +
                "explicitly after emptying the room; the saved request was preserved.");
            return;
        }

        string expectedCustomMap = GetManagedMapAssetKey(interior, building);
        if (AssetNamesEqual(indoors.mapPath.Value, expectedCustomMap))
        {
            if (Context.IsMainPlayer)
            {
                SelectionStorage.ClearRequiresEmptyRestore(
                    building,
                    stored.Selection.TargetContract);
            }

            return;
        }

        if (!Context.IsMainPlayer)
        {
            return;
        }

        bool requiresEmptyRestore = !allowPopulatedExactRestore
            || SelectionStorage.RequiresEmptyRestore(
                building,
                stored.Selection.TargetContract);
        if (!allowPopulatedExactRestore)
        {
            SelectionStorage.MarkRequiresEmptyRestore(
                building,
                stored.Selection.TargetContract);
        }

        SwitchSafetyResult customSafety = requiresEmptyRestore
            ? SwitchSafetyInspector.Inspect(building, indoors, target)
            : SwitchSafetyInspector.InspectExactSaveRestore(building, indoors);
        if (!customSafety.IsSafe)
        {
            SelectionStorage.MarkRequiresEmptyRestore(
                building,
                stored.Selection.TargetContract);
            ReportSelectionIssueOnce(
                building,
                $"Couldn't reconcile '{custom.VariantId}' safely: {customSafety.ToUserMessage()}.");
            return;
        }

        SelectionDataSnapshot previousSelection =
            SelectionStorage.Capture(building, stored.Selection.TargetContract);
        if (ApplyAndBroadcastTrusted(
                building,
                indoors,
                target,
                expectedCustomMap,
                stored.Selection.Choice,
                commitState: () => SelectionStorage.ClearRequiresEmptyRestore(
                    building,
                    stored.Selection.TargetContract),
                rollbackState: () => SelectionStorage.Restore(building, previousSelection)))
        {
            return;
        }

        SelectionStorage.MarkRequiresEmptyRestore(
            building,
            stored.Selection.TargetContract);
    }

    private bool ApplyAndBroadcastTrusted(
        Building building,
        GameLocation indoors,
        InteriorTarget target,
        string mapAssetKey,
        InteriorChoice choice,
        Action? commitState = null,
        Action? rollbackState = null)
    {
        if (TryApplyMapAndStateAtomically(
                building,
                indoors,
                target,
                mapAssetKey,
                commitState ?? (static () => { }),
                rollbackState ?? (static () => { }),
                out Exception? applyFailure,
                out Exception? rollbackFailure))
        {
            BroadcastCommittedSelection(building, choice);
            return true;
        }

        Monitor.Log(
            $"Couldn't reconcile interior for building {building.id.Value:D}. " +
            $"{applyFailure}",
            LogLevel.Error);
        if (rollbackFailure is not null)
        {
            Monitor.Log(
                $"Interior rollback also failed for building {building.id.Value:D}. " +
                $"{rollbackFailure}",
                LogLevel.Error);
        }

        return false;
    }

    private bool TryApplyMapAndStateAtomically(
        Building building,
        GameLocation indoors,
        InteriorTarget target,
        string mapAssetKey,
        Action commitState,
        Action rollbackState,
        out Exception? applyFailure,
        out Exception? rollbackFailure)
    {
        string previousMap = indoors.mapPath.Value;
        bool mapChanged = !AssetNamesEqual(previousMap, mapAssetKey);
        applyFailure = null;
        rollbackFailure = null;

        try
        {
            if (mapChanged)
            {
                ApplyResolvedMap(building, indoors, target, mapAssetKey, setMapPath: true);
            }

            commitState();
            return true;
        }
        catch (Exception exception)
        {
            applyFailure = exception;
        }

        var rollbackFailures = new List<Exception>();
        try
        {
            rollbackState();
        }
        catch (Exception exception)
        {
            rollbackFailures.Add(exception);
        }

        try
        {
            if (mapChanged && !AssetNamesEqual(indoors.mapPath.Value, previousMap))
            {
                ApplyResolvedMap(building, indoors, target, previousMap, setMapPath: true);
            }
        }
        catch (Exception exception)
        {
            rollbackFailures.Add(exception);
        }

        rollbackFailure = rollbackFailures.Count switch
        {
            0 => null,
            1 => rollbackFailures[0],
            _ => new AggregateException(rollbackFailures),
        };
        return false;
    }

    private void BroadcastCommittedSelection(Building building, InteriorChoice choice)
    {
        var message = new SelectionCommittedMessage
        {
            BuildingId = building.id.Value.ToString("D"),
        };
        if (choice is InteriorChoice.CustomChoice custom)
        {
            message.VariantId = custom.VariantId.Value;
            message.ContentHash = custom.ContentHash.Value;
        }

        Helper.Multiplayer.SendMessage(
            message,
            MultiplayerMessageTypes.SelectionCommitted,
            modIDs: new[] { ModManifest.UniqueID });
    }

    private void ApplyResolvedMap(
        Building building,
        GameLocation indoors,
        InteriorTarget target,
        string mapAssetKey,
        bool setMapPath)
    {
        bool isManagedMap = catalog.TryGetManagedMapTarget(mapAssetKey, out _);
        try
        {
            if (isManagedMap)
            {
                if (!Context.IsOnHostComputer)
                {
                    clientResolvedProxyModes.Remove(NormalizeAssetName(mapAssetKey));
                }

                Helper.GameContent.InvalidateCache(mapAssetKey);
            }

            xTile.Map resolvedMap = Helper.GameContent.Load<xTile.Map>(mapAssetKey);
            if (!MapContractValidator.TryValidate(target, resolvedMap, out string reason))
            {
                throw new InvalidOperationException(
                    $"Resolved map '{mapAssetKey}' violates the {target} runtime contract: {reason}");
            }

            if (setMapPath)
            {
                indoors.mapPath.Value = mapAssetKey;
                indoors.updateMap();
            }
            else
            {
                // Always force the final local load. SMAPI may have propagated the
                // invalidation already, but its bool result can't attest that this
                // specific location reloaded successfully. A normal reloadMap() could
                // also reuse a map snapshot supplied by the multiplayer host.
                indoors.loadMap(mapAssetKey, true);
            }
            building.updateInteriorWarps(indoors);
        }
        catch
        {
            clientReloadedMaps.Remove(building.id.Value);
            if (!Context.IsOnHostComputer && isManagedMap)
            {
                clientResolvedProxyModes.Remove(NormalizeAssetName(mapAssetKey));
            }

            throw;
        }
    }

    private void ReloadSynchronizedMap(
        Building building,
        GameLocation indoors,
        InteriorTarget target) =>
        ApplyResolvedMap(
            building,
            indoors,
            target,
            indoors.mapPath.Value,
            setMapPath: false);

    private bool TryResolveCommandBuilding(
        string? token,
        out Building building,
        out InteriorTarget target)
    {
        building = null!;
        target = default;
        if (!RequireWorldReady())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            building = FindCurrentBuilding()!;
            if (building is null || Classify(building) is not { } currentTarget)
            {
                Monitor.Log(
                    "Enter a supported interior, or pass a building ID from 'sic targets'.",
                    LogLevel.Error);
                return false;
            }

            target = currentTarget;
            return true;
        }

        Building[] matches;
        if (string.Equals(token, "greenhouse", StringComparison.OrdinalIgnoreCase))
        {
            matches = GetFarmBuildings()
                .Where(candidate => Classify(candidate) == InteriorTarget.Greenhouse)
                .ToArray();
        }
        else if (Guid.TryParse(token, out Guid id))
        {
            matches = GetFarmBuildings()
                .Where(candidate => candidate.id.Value == id)
                .ToArray();
        }
        else
        {
            string normalized = token.Replace("-", string.Empty, StringComparison.Ordinal);
            matches = GetFarmBuildings()
                .Where(candidate => candidate.id.Value.ToString("N")
                    .StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        if (matches.Length != 1 || Classify(matches[0]) is not { } classified)
        {
            Monitor.Log(
                matches.Length > 1
                    ? $"Building ID prefix '{token}' is ambiguous."
                    : $"No supported building matches '{token}'. Run 'sic targets'.",
                LogLevel.Error);
            return false;
        }

        building = matches[0];
        target = classified;
        return true;
    }

    private bool RequireWorldReady()
    {
        if (Context.IsWorldReady)
        {
            return true;
        }

        Monitor.Log("Load a save before using this command.", LogLevel.Error);
        return false;
    }

    private void ReportSelectionIssueOnce(Building building, string message)
    {
        string key = $"{building.id.Value:N}:{message}";
        if (reportedSelectionIssues.Add(key))
        {
            Monitor.Log($"Building {building.id.Value:D}: {message}", LogLevel.Error);
        }
    }

    private static bool TryResolveVanillaMap(Building building, out string mapAssetKey)
    {
        mapAssetKey = building.GetData()?.IndoorMap?.Trim() ?? string.Empty;
        if (mapAssetKey.Length == 0)
        {
            return false;
        }

        mapAssetKey = mapAssetKey.Replace('\\', '/');
        if (!mapAssetKey.Contains('/'))
        {
            mapAssetKey = $"Maps/{mapAssetKey}";
        }

        return true;
    }

    private static bool ChoicesEqual(InteriorChoice left, InteriorChoice right) =>
        left switch
        {
            InteriorChoice.VanillaChoice => right is InteriorChoice.VanillaChoice,
            InteriorChoice.CustomChoice customLeft when right is InteriorChoice.CustomChoice customRight =>
                customLeft.VariantId == customRight.VariantId
                && customLeft.ContentHash == customRight.ContentHash,
            _ => false,
        };

    private static string FormatChoice(InteriorChoice choice) => choice switch
    {
        InteriorChoice.VanillaChoice => "vanilla",
        InteriorChoice.CustomChoice custom =>
            $"'{custom.VariantId.Value}' ({ShortHash(custom.ContentHash)})",
        _ => "<unknown>",
    };

    private static bool HasOnlinePlayerInside(GameLocation indoors) =>
        Game1.getOnlineFarmers().Any(farmer =>
            ReferenceEquals(farmer.currentLocation, indoors)
            || string.Equals(
                farmer.currentLocation?.NameOrUniqueName,
                indoors.NameOrUniqueName,
                StringComparison.Ordinal));

    private static bool AssetNamesEqual(string? left, string? right) =>
        string.Equals(
            left?.Replace('\\', '/'),
            right?.Replace('\\', '/'),
            StringComparison.OrdinalIgnoreCase);

    private static string GetManagedMapAssetKey(
        RuntimeInterior interior,
        Building building)
    {
        string instanceToken = interior.Definition.Target switch
        {
            InteriorTarget.Greenhouse => "greenhouse",
            InteriorTarget.DeluxeBarn => building.id.Value.ToString("N"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(interior),
                interior.Definition.Target,
                "Unknown managed interior target."),
        };
        return $"{interior.MapAssetKey}/Instances/{instanceToken}";
    }

    private bool WasClientMapResolvedAsCustom(string mapAssetKey) =>
        catalog.TryGetManagedMapTarget(mapAssetKey, out _)
        && clientResolvedProxyModes.TryGetValue(
            NormalizeAssetName(mapAssetKey),
            out bool loadedCustom)
        && loadedCustom;

    private static string GetClientReloadStateKey(string mapAssetKey, bool loadedCustom) =>
        mapAssetKey + (loadedCustom ? "#custom" : "#fallback");

    private static string NormalizeAssetName(string value) =>
        value.Replace('\\', '/').Trim('/');

    private static Building? FindCurrentBuilding()
    {
        if (!Context.IsWorldReady)
        {
            return null;
        }

        GameLocation currentLocation = Game1.currentLocation;
        return GetFarmBuildings().FirstOrDefault(building =>
            ReferenceEquals(building.GetIndoors(), currentLocation)
            || string.Equals(
                building.GetIndoors()?.NameOrUniqueName,
                currentLocation.NameOrUniqueName,
                StringComparison.Ordinal));
    }

    private static Building? FindBuilding(Guid id) =>
        GetFarmBuildings().FirstOrDefault(building => building.id.Value == id);

    private static IEnumerable<Building> GetFarmBuildings() =>
        Game1.getFarm().buildings;

    private static InteriorTarget? Classify(Building building) =>
        building.buildingType.Value switch
        {
            "Greenhouse" => InteriorTarget.Greenhouse,
            "Deluxe Barn" => InteriorTarget.DeluxeBarn,
            _ => null,
        };

    private static string ShortHash(ContentHash hash) =>
        hash.Value[..Math.Min(12, hash.Value.Length)];
}
