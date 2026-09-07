using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewInteriorChanger.Core;
using StardewModdingAPI;
using StardewUI.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace StardewInteriorChanger;

internal sealed record InteriorMenuBuilding(Building Building, InteriorTarget Target, string Label);

internal sealed class InteriorMenuCard : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public InteriorMenuOption Option { get; }
    public int Index { get; }
    public string Name { get; }
    public string Source { get; }
    public string Tag => $"variant-{Index}";
    public bool IsCurrent => Option.IsCurrent;
    public bool IsSelected { get; private set; }
    public Color BackgroundTint => IsSelected ? new Color(207, 225, 179) : new Color(249, 231, 195);
    public string SelectionMark => IsSelected ? ">" : string.Empty;

    public InteriorMenuCard(InteriorMenuOption option, int index, string name, string source)
    {
        Option = option;
        Index = index;
        Name = name;
        Source = source;
    }

    public void SetSelected(bool selected)
    {
        if (IsSelected == selected) return;
        IsSelected = selected;
        PropertyChanged?.Invoke(this, new(nameof(BackgroundTint)));
        PropertyChanged?.Invoke(this, new(nameof(SelectionMark)));
    }
}

/// <summary>Owns one menu session; gameplay requests still use the mod's shared selection pipeline.</summary>
internal sealed class InteriorSelectionMenu : INotifyPropertyChanged, IDisposable
{
    public const string ViewAsset = "Mods/StardewInteriorChanger.Core/Views/InteriorSelection";
    private readonly ModEntry mod;
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly IInteriorCatalog catalog;
    private readonly InteriorMenuBuilding[] buildings;
    private readonly IMenuController controller;
    private InteriorMenuView view = null!;
    private int selectedBuildingIndex;
    private int selectedVariantIndex;
    private int viewportWidth;
    private int viewportHeight;
    private bool disposed;
    private bool previewFailed;
    private string? loadedPreviewKey;
    private string feedback = string.Empty;
    private bool rejected;
    private IReadOnlyList<InteriorMenuCard> cards = Array.Empty<InteriorMenuCard>();
    private Texture2D? preview;

    public event PropertyChangedEventHandler? PropertyChanged;
    public IClickableMenu Menu => controller.Menu;
    public bool IsPending { get; private set; }
    public bool CanChoose => !IsPending;
    public bool CanApply => !IsPending && !SelectedOption.IsCurrent;
    public Color ApplyTint => CanApply ? new Color(207, 225, 179) : new Color(205, 200, 188);
    public Color StatusColor => rejected ? new Color(151, 50, 35) : new Color(62, 83, 47);
    public string Status => string.IsNullOrEmpty(feedback)
        ? T(HasSourceConfigurationUpdate ? "menu.status.settings-changed" : "menu.status.ready") : feedback;
    public string ApplyLabel => IsPending ? T("menu.apply.pending") : SelectedOption.IsCurrent
        ? T("menu.apply.current") : T(IsUpdatedConfigurationSelection ? "menu.apply.settings" : "menu.apply");
    public IReadOnlyList<string> BuildingLabels { get; }
    public IReadOnlyList<InteriorMenuCard> Cards => cards;
    public string SelectedName => Cards[selectedVariantIndex].Name;
    public string SelectedSource => Cards[selectedVariantIndex].Source;
    public bool SelectedIsCurrent => SelectedOption.IsCurrent;
    public string InteriorCount => T("menu.count", new { count = Cards.Count });
    public string Warning => FormatWarning(view.Warning);
    public bool HasWarning => view.Warning.Kind != InteriorMenuWarningKind.None;
    public Texture2D? Preview => preview;
    public bool HasPreview => Preview is not null;
    public bool HasNoPreview => !HasPreview;
    public string PreviewMessage => T(SelectedOption.IsBase ? "menu.preview.base" : previewFailed ? "menu.preview.failed" : "menu.preview.none");
    public string Description => SelectedOption.IsBase ? T("menu.base.description") : T("menu.custom.description");
    public bool Compact => viewportWidth < 900;
    public string RootLayout => $"{Math.Max(240, Math.Min(1080, viewportWidth - 144))}px content";
    public string BodyLayout => $"stretch {Math.Max(140, Math.Min(440, viewportHeight - 380))}px";
    public string BodyOrientation => Compact ? "vertical" : "horizontal";
    public string ListLayout => Compact ? "stretch 42%" : "40% stretch";
    public string PreviewMargin => Compact ? "0, 12, 0, 0" : "16, 0, 0, 0";
    public string BuildingLayout => $"{Math.Max(120, Math.Min(680, viewportWidth - 300))}px content";
    public string PreviewImageLayout => Compact ? "stretch 90px" : "stretch 220px";

    public int SelectedBuildingIndex
    {
        get => selectedBuildingIndex;
        set
        {
            if (value == selectedBuildingIndex || value < 0 || value >= buildings.Length) return;
            if (IsPending) { ShowPendingBlocked(); Notify(nameof(SelectedBuildingIndex)); return; }
            selectedBuildingIndex = value;
            feedback = string.Empty;
            rejected = false;
            RefreshView();
            SelectCurrent();
        }
    }

    public string SelectedBuildingLabel
    {
        get => BuildingLabels[selectedBuildingIndex];
        set => SelectedBuildingIndex = Array.FindIndex(buildings, item => item.Label == value);
    }

    private InteriorMenuBuilding SelectedBuilding => buildings[selectedBuildingIndex];
    private InteriorMenuOption SelectedOption => view.Options[selectedVariantIndex];

    private RuntimeInterior? CurrentSourceInterior
    {
        get
        {
            string? currentId = view.Options.FirstOrDefault(option => option.IsCurrent)?.VariantId;
            return currentId is not null && catalog.TryGet(currentId, out RuntimeInterior current)
                && current.SourceFamilyId is not null ? current : null;
        }
    }

    private bool HasSourceConfigurationUpdate => CurrentSourceInterior is { } current
        && !current.IsCurrentSourceConfiguration
        && catalog.Entries.Any(entry => entry.SourceFamilyId == current.SourceFamilyId
            && entry.IsCurrentSourceConfiguration);

    private bool IsUpdatedConfigurationSelection => HasSourceConfigurationUpdate
        && SelectedOption.VariantId is { } id && catalog.TryGet(id, out RuntimeInterior selected)
        && selected.IsCurrentSourceConfiguration
        && selected.SourceFamilyId == CurrentSourceInterior!.SourceFamilyId;

    public InteriorSelectionMenu(ModEntry mod, IModHelper helper, IMonitor monitor, IInteriorCatalog catalog,
        IViewEngine viewEngine, IReadOnlyList<(Building Building, InteriorTarget Target)> supportedBuildings,
        Guid? initiallySelectedBuilding, InteriorMenuRequest? pendingRequest)
    {
        this.mod = mod;
        this.helper = helper;
        this.monitor = monitor;
        this.catalog = catalog;
        buildings = supportedBuildings.Select(item => new InteriorMenuBuilding(item.Building, item.Target,
            T(item.Target switch
            {
                InteriorTarget.Greenhouse => "menu.building.greenhouse",
                InteriorTarget.Barn => "menu.building.basic-barn",
                InteriorTarget.BigBarn => "menu.building.big-barn",
                InteriorTarget.DeluxeBarn => "menu.building.barn",
                InteriorTarget.Coop => "menu.building.coop",
                InteriorTarget.BigCoop => "menu.building.big-coop",
                InteriorTarget.DeluxeCoop => "menu.building.deluxe-coop",
                InteriorTarget.Shed => "menu.building.shed",
                InteriorTarget.BigShed => "menu.building.big-shed",
                _ => throw new ArgumentOutOfRangeException(nameof(item.Target))
            }) + " · " +
            T("menu.building.position", new { x = item.Building.tileX.Value, y = item.Building.tileY.Value }))).ToArray();
        BuildingLabels = buildings.Select(item => item.Label).ToArray();
        Guid? requestedId = pendingRequest is not null && Guid.TryParse(pendingRequest.BuildingId, out Guid pendingId)
            ? pendingId : initiallySelectedBuilding;
        selectedBuildingIndex = Math.Max(0, Array.FindIndex(buildings, item => item.Building.id.Value == requestedId));
        RefreshView();
        SelectCurrent();
        if (pendingRequest is not null)
        {
            int pendingIndex = view.Options.ToList().FindIndex(option => option.VariantId == pendingRequest.VariantId);
            if (pendingIndex >= 0) SelectVariant(pendingIndex);
            ShowPending();
        }
        viewportWidth = Game1.uiViewport.Width;
        viewportHeight = Game1.uiViewport.Height;
        controller = viewEngine.CreateMenuControllerFromAsset(ViewAsset, this);
        controller.EnableCloseButton();
        controller.CloseButtonOffset = new Vector2(-64, 28);
        controller.DefaultFocusableTag = $"variant-{selectedVariantIndex}";
        controller.FocusOnTaggedView(controller.DefaultFocusableTag);
    }

    public void RefreshLayout()
    {
        if (disposed || (viewportWidth == Game1.uiViewport.Width && viewportHeight == Game1.uiViewport.Height)) return;
        viewportWidth = Game1.uiViewport.Width;
        viewportHeight = Game1.uiViewport.Height;
        foreach (string property in new[] { nameof(RootLayout), nameof(BodyLayout), nameof(BodyOrientation), nameof(ListLayout),
            nameof(PreviewMargin), nameof(BuildingLayout), nameof(PreviewImageLayout) }) Notify(property);
    }

    public void RefreshCatalog()
    {
        if (disposed || IsPending) return;
        string? selectedId = SelectedOption.VariantId;
        RefreshView();
        int selected = view.Options.ToList().FindIndex(option => option.VariantId == selectedId);
        if (selected < 0) selected = view.Options.ToList().FindIndex(option => option.IsCurrent);
        SelectVariant(Math.Max(0, selected));
    }

    public void SelectVariant(int index)
    {
        if (disposed || index < 0 || index >= view.Options.Count) return;
        if (IsPending) { ShowPendingBlocked(); return; }
        selectedVariantIndex = index;
        feedback = string.Empty;
        rejected = false;
        foreach (InteriorMenuCard card in Cards) card.SetSelected(card.Index == index);
        LoadSelectedPreview();
        NotifyState();
    }

    public void ApplySelection()
    {
        if (disposed) return;
        if (IsPending) { ShowPendingBlocked(); return; }
        if (!CanApply) return;
        mod.RequestMenuSelection(this, SelectedBuilding.Building, SelectedBuilding.Target, SelectedOption.VariantId);
    }

    public void ShowPending()
    {
        IsPending = true;
        feedback = T("menu.status.pending");
        rejected = false;
        NotifyState();
    }

    public void ShowPendingBlocked()
    {
        feedback = T("menu.status.pending-blocked");
        NotifyState();
    }

    public void HandleSelectionResult(bool success, string buildingId, string? variantId, string message)
    {
        if (disposed) return;
        IsPending = false;
        RefreshView(success && SelectedBuilding.Building.id.Value.ToString("D") == buildingId, variantId);
        int index = view.Options.ToList().FindIndex(option => option.VariantId == variantId);
        selectedVariantIndex = Math.Clamp(index >= 0 ? index : selectedVariantIndex, 0, view.Options.Count - 1);
        foreach (InteriorMenuCard card in Cards) card.SetSelected(card.Index == selectedVariantIndex);
        feedback = T(success ? "menu.status.applied" : "menu.status.rejected", new { message });
        rejected = !success;
        LoadSelectedPreview();
        NotifyState();
    }

    private void SelectCurrent()
    {
        int current = view.Options.ToList().FindIndex(option => option.IsCurrent);
        SelectVariant(Math.Max(0, current));
    }

    private void RefreshView(bool confirmed = false, string? variantId = null)
    {
        SelectionReadResult stored = SelectionStorage.Read(SelectedBuilding.Building, SelectedBuilding.Target);
        InteriorMenuStoredChoice choice = confirmed
            ? variantId is null ? InteriorMenuStoredChoice.Base()
                : catalog.TryGet(variantId, out RuntimeInterior entry)
                    ? InteriorMenuStoredChoice.Custom(entry.Definition.Id, entry.Definition.ContentHash)
                    : InteriorMenuStoredChoice.Custom(VariantId.Parse(variantId), ContentHash.Parse(new string('0', 64)))
            : !stored.IsValid ? InteriorMenuStoredChoice.Invalid(stored.Error ?? T("menu.warning.unknown-data"))
                : stored.Selection.Choice switch
                {
                    InteriorChoice.VanillaChoice => InteriorMenuStoredChoice.Base(),
                    InteriorChoice.CustomChoice custom => InteriorMenuStoredChoice.Custom(custom.VariantId, custom.ContentHash),
                    _ => InteriorMenuStoredChoice.Invalid(T("menu.warning.unknown-data"))
                };
        view = InteriorMenuStateBuilder.Build(SelectedBuilding.Target, catalog.Entries
            .Where(entry => entry.SourceFamilyId is null || entry.IsCurrentSourceConfiguration
                || entry.Definition.Id == choice.VariantId)
            .Select(entry =>
            new InteriorMenuVariant(entry.Definition.Id, entry.Definition.DisplayName, entry.Definition.Target,
                entry.Definition.ContentHash, entry.SourcePackId, entry.SourcePackVersion, entry.PreviewAssetKey)), choice);
        selectedVariantIndex = Math.Clamp(selectedVariantIndex, 0, view.Options.Count - 1);
        cards = view.Options.Select((option, index) => new InteriorMenuCard(option, index,
            option.IsBase ? T("menu.base.name") : option.DisplayName,
            option.IsBase ? T("menu.base.source") : T("menu.source", new { pack = option.SourcePackId, version = option.SourcePackVersion }))).ToArray();
        Notify(nameof(Cards));
        Notify(nameof(InteriorCount));
    }

    private string FormatWarning(InteriorMenuWarning warning) => warning.Kind switch
    {
        InteriorMenuWarningKind.InvalidStoredSelection => T("menu.warning.invalid", new { detail = warning.Detail ?? T("menu.warning.unknown-data") }),
        InteriorMenuWarningKind.MissingVariant => T("menu.warning.missing", new { variant = warning.VariantId ?? T("menu.warning.unknown-variant") }),
        InteriorMenuWarningKind.ContentHashMismatch => T("menu.warning.hash", new { variant = warning.VariantId ?? T("menu.warning.unknown-variant") }),
        _ => string.Empty
    };

    private void LoadSelectedPreview()
    {
        string? key = SelectedOption.PreviewAssetKey;
        if (key == loadedPreviewKey && (Preview is not null || previewFailed)) return;
        loadedPreviewKey = key;
        preview = null;
        previewFailed = false;
        if (string.IsNullOrWhiteSpace(key)) return;
        try { preview = helper.GameContent.Load<Texture2D>(key); }
        catch (Exception exception)
        {
            previewFailed = true;
            monitor.Log($"Couldn't load optional interior preview '{key}'. The variant remains usable. {exception.Message}", LogLevel.Warn);
        }
    }

    private string T(string key, object? tokens = null) => helper.Translation.Get(key, tokens);
    private void Notify(string name) => PropertyChanged?.Invoke(this, new(name));
    private void NotifyState()
    {
        if (controller is not null) controller.DefaultFocusableTag = $"variant-{selectedVariantIndex}";
        foreach (string property in new[] { nameof(IsPending), nameof(CanChoose), nameof(CanApply), nameof(ApplyTint),
            nameof(Status), nameof(StatusColor), nameof(ApplyLabel), nameof(SelectedName), nameof(SelectedSource),
            nameof(SelectedIsCurrent), nameof(Warning), nameof(HasWarning), nameof(Preview), nameof(HasPreview),
            nameof(HasNoPreview), nameof(PreviewMessage), nameof(Description), nameof(SelectedBuildingIndex), nameof(SelectedBuildingLabel) }) Notify(property);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        controller.Dispose();
        // SMAPI owns the cached preview texture; the menu must not dispose it.
        preview = null;
    }
}
