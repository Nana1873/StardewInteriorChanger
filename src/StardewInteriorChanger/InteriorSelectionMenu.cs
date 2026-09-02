using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewInteriorChanger.Core;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace StardewInteriorChanger;

internal sealed record InteriorMenuBuilding(
    Building Building,
    InteriorTarget Target,
    string Label,
    string Detail);

internal enum InteriorMenuFocus
{
    Buildings,
    Variants,
    Apply
}

internal enum InteriorMenuFeedback
{
    None,
    Pending,
    Applied,
    Rejected
}

internal sealed class InteriorSelectionMenu : IClickableMenu
{
    private const int BuildingComponentBase = 1000;
    private const int VariantComponentBase = 2000;
    private const int ApplyComponentId = 3000;
    private const int RowHeight = 68;
    private const int Gap = 12;
    private readonly ModEntry mod;
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly IInteriorCatalog catalog;
    private readonly List<InteriorMenuBuilding> buildings;
    private InteriorMenuView view = null!;
    private Rectangle buildingListBounds;
    private Rectangle variantListBounds;
    private Rectangle previewBounds;
    private Rectangle applyBounds;
    private Rectangle statusBounds;
    private int variantRowsTopInset;
    private int viewportWidth;
    private int viewportHeight;
    private int selectedBuildingIndex;
    private int selectedVariantIndex;
    private int buildingScroll;
    private int variantScroll;
    private int hoveredComponentId = -1;
    private InteriorMenuFocus focus = InteriorMenuFocus.Variants;
    private InteriorMenuFeedback feedback;
    private string feedbackMessage = string.Empty;
    private Texture2D? previewTexture;
    private string? loadedPreviewKey;
    private bool previewFailed;

    public InteriorSelectionMenu(
        ModEntry mod,
        IModHelper helper,
        IMonitor monitor,
        IInteriorCatalog catalog,
        IReadOnlyList<(Building Building, InteriorTarget Target)> supportedBuildings,
        Guid? initiallySelectedBuilding)
        : base(0, 0, 0, 0, showUpperRightCloseButton: true)
    {
        this.mod = mod;
        this.helper = helper;
        this.monitor = monitor;
        this.catalog = catalog;
        buildings = supportedBuildings
            .Select(item => new InteriorMenuBuilding(
                item.Building,
                item.Target,
                FormatBuildingLabel(item.Target),
                FormatBuildingDetail(item.Building, item.Target)))
            .ToList();

        int initialIndex = initiallySelectedBuilding is null
            ? -1
            : buildings.FindIndex(item => item.Building.id.Value == initiallySelectedBuilding.Value);
        selectedBuildingIndex = initialIndex >= 0
            ? initialIndex
            : Math.Max(0, buildings.FindIndex(item => item.Target == InteriorTarget.Greenhouse));

        RefreshView();
        int current = view.Options.ToList().FindIndex(option => option.IsCurrent);
        selectedVariantIndex = current >= 0 ? current : 0;
        LoadSelectedPreview();
        RebuildLayout();
        if (Game1.options.SnappyMenus)
        {
            snapToDefaultClickableComponent();
        }
    }

    public bool IsPending => feedback == InteriorMenuFeedback.Pending;

    public void ShowPending()
    {
        feedback = InteriorMenuFeedback.Pending;
        feedbackMessage = helper.Translation.Get("menu.status.pending");
    }

    public void ShowPendingBlocked()
    {
        feedback = InteriorMenuFeedback.Pending;
        feedbackMessage = helper.Translation.Get("menu.status.pending-blocked");
    }

    public void HandleSelectionResult(bool success, string buildingId, string? variantId, string message)
    {
        feedback = success ? InteriorMenuFeedback.Applied : InteriorMenuFeedback.Rejected;
        feedbackMessage = helper.Translation.Get(
            success ? "menu.status.applied" : "menu.status.rejected",
            new { message });

        if (!success
            || !Guid.TryParse(buildingId, out Guid id)
            || buildings[selectedBuildingIndex].Building.id.Value != id)
        {
            RefreshView();
            return;
        }

        RefreshView(variantId, useConfirmedChoice: true);
        selectedVariantIndex = Math.Max(
            0,
            view.Options.ToList().FindIndex(option =>
                string.Equals(option.VariantId, variantId, StringComparison.Ordinal)));
        EnsureVariantVisible();
        LoadSelectedPreview();
        RefreshClickableComponents();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        RebuildLayout();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (upperRightCloseButton?.containsPoint(x, y) == true)
        {
            exitThisMenu();
            return;
        }

        ClickableComponent? clicked = allClickableComponents.FirstOrDefault(
            component => component.containsPoint(x, y));
        if (clicked is null)
        {
            return;
        }

        if (clicked.myID is >= BuildingComponentBase and < VariantComponentBase)
        {
            if (IsPending)
            {
                ShowPendingBlocked();
                return;
            }

            SelectBuilding(clicked.myID - BuildingComponentBase);
            focus = InteriorMenuFocus.Buildings;
            Game1.playSound("smallSelect");
            return;
        }

        if (clicked.myID is >= VariantComponentBase and < ApplyComponentId)
        {
            if (IsPending)
            {
                ShowPendingBlocked();
                return;
            }

            SelectVariant(clicked.myID - VariantComponentBase);
            focus = InteriorMenuFocus.Variants;
            Game1.playSound("smallSelect");
            return;
        }

        if (clicked.myID == ApplyComponentId)
        {
            focus = InteriorMenuFocus.Apply;
            ApplySelection();
        }
    }

    public override void receiveScrollWheelAction(int direction)
    {
        Point mouse = Game1.getMousePosition(true);
        if (buildingListBounds.Contains(mouse))
        {
            buildingScroll = Scroll(buildingScroll, direction, buildings.Count, VisibleBuildingRows);
            RefreshClickableComponents();
            return;
        }

        if (variantListBounds.Contains(mouse))
        {
            variantScroll = Scroll(variantScroll, direction, view.Options.Count, VisibleVariantRows);
            RefreshClickableComponents();
        }
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            exitThisMenu();
            return;
        }

        if (key is Keys.Tab or Keys.Left or Keys.Right)
        {
            MoveFocus(key == Keys.Left ? -1 : 1);
            return;
        }

        if (key is Keys.Up or Keys.Down)
        {
            MoveSelection(key == Keys.Up ? -1 : 1);
            return;
        }

        if (key is Keys.Enter or Keys.Space)
        {
            if (focus == InteriorMenuFocus.Buildings)
            {
                MoveFocus(1);
            }
            else
            {
                ApplySelection();
            }

            return;
        }

        base.receiveKeyPress(key);
    }

    public override void receiveGamePadButton(Buttons button)
    {
        if (button == Buttons.B)
        {
            exitThisMenu();
            return;
        }

        if (button == Buttons.A && currentlySnappedComponent is not null)
        {
            Point center = currentlySnappedComponent.bounds.Center;
            receiveLeftClick(center.X, center.Y);
            return;
        }

        if (button is Buttons.DPadUp or Buttons.LeftThumbstickUp)
        {
            MoveSelection(-1);
            return;
        }

        if (button is Buttons.DPadDown or Buttons.LeftThumbstickDown)
        {
            MoveSelection(1);
            return;
        }

        if (button is Buttons.DPadLeft or Buttons.LeftThumbstickLeft)
        {
            MoveFocus(-1);
            return;
        }

        if (button is Buttons.DPadRight or Buttons.LeftThumbstickRight)
        {
            MoveFocus(1);
            return;
        }

        base.receiveGamePadButton(button);
        SyncFocusFromSnappedComponent();
    }

    public override void performHoverAction(int x, int y)
    {
        hoveredComponentId = allClickableComponents.FirstOrDefault(
            component => component.containsPoint(x, y))?.myID ?? -1;
        base.performHoverAction(x, y);
    }

    public override void snapToDefaultClickableComponent()
    {
        int preferred = view.Options.Count > 0
            ? VariantComponentBase + selectedVariantIndex
            : BuildingComponentBase + selectedBuildingIndex;
        setCurrentlySnappedComponentTo(preferred);
        snapCursorToCurrentSnappedComponent();
    }

    public override void draw(SpriteBatch b)
    {
        if (viewportWidth != Game1.uiViewport.Width || viewportHeight != Game1.uiViewport.Height)
        {
            RebuildLayout();
        }

        drawBackground(b);
        drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);
        DrawText(
            b,
            helper.Translation.Get("menu.title"),
            new Vector2(xPositionOnScreen + 28, yPositionOnScreen + 20),
            Game1.textColor,
            Game1.dialogueFont);

        DrawPanel(b, buildingListBounds, helper.Translation.Get("menu.buildings"));
        DrawPanel(b, variantListBounds, helper.Translation.Get("menu.variants"));
        DrawPanel(b, previewBounds, helper.Translation.Get("menu.preview"));
        DrawBuildings(b);
        DrawVariants(b);
        DrawPreview(b);
        DrawStatusAndApply(b);

        upperRightCloseButton?.draw(b);
        drawMouse(b);
    }

    private int VisibleBuildingRows => Math.Max(1, (buildingListBounds.Height - 44) / RowHeight);

    private int VisibleVariantRows => Math.Max(
        1,
        (variantListBounds.Height - variantRowsTopInset - 12) / RowHeight);

    private InteriorMenuBuilding SelectedBuilding => buildings[selectedBuildingIndex];

    private InteriorMenuOption SelectedOption => view.Options[selectedVariantIndex];

    private void RebuildLayout()
    {
        viewportWidth = Game1.uiViewport.Width;
        viewportHeight = Game1.uiViewport.Height;
        width = Math.Min(1120, Math.Max(1, viewportWidth - 24));
        height = Math.Min(680, Math.Max(1, viewportHeight - 24));
        xPositionOnScreen = (viewportWidth - width) / 2;
        yPositionOnScreen = (viewportHeight - height) / 2;

        const int outerPadding = 24;
        const int headerHeight = 64;
        const int footerHeight = 86;
        int contentY = yPositionOnScreen + headerHeight;
        int contentHeight = Math.Max(220, height - headerHeight - footerHeight);
        int contentWidth = width - (outerPadding * 2);
        int buildingWidth = Math.Clamp((int)(contentWidth * 0.27f), 210, 280);
        int previewWidth = Math.Clamp((int)(contentWidth * 0.29f), 210, 310);
        int variantWidth = Math.Max(230, contentWidth - buildingWidth - previewWidth - (Gap * 2));

        buildingListBounds = new Rectangle(
            xPositionOnScreen + outerPadding,
            contentY,
            buildingWidth,
            contentHeight);
        variantListBounds = new Rectangle(
            buildingListBounds.Right + Gap,
            contentY,
            variantWidth,
            contentHeight);
        previewBounds = new Rectangle(
            variantListBounds.Right + Gap,
            contentY,
            previewWidth,
            contentHeight);

        string baseHelp = Game1.parseText(
            helper.Translation.Get("menu.base.description"),
            Game1.tinyFont,
            variantListBounds.Width - 28);
        variantRowsTopInset = Math.Max(
            104,
            38 + (int)Math.Ceiling(Game1.tinyFont.MeasureString(baseHelp).Y) + 12);

        int footerY = contentY + contentHeight + 10;
        applyBounds = new Rectangle(xPositionOnScreen + width - 246, footerY, 210, 52);
        statusBounds = new Rectangle(
            xPositionOnScreen + outerPadding,
            footerY,
            Math.Max(160, applyBounds.Left - xPositionOnScreen - outerPadding - 16),
            58);

        initializeUpperRightCloseButton();
        ClampScrolls();
        RefreshClickableComponents();
    }

    private void RefreshView(string? confirmedVariantId = null, bool useConfirmedChoice = false)
    {
        SelectionReadResult stored = SelectionStorage.Read(
            SelectedBuilding.Building,
            SelectedBuilding.Target);
        InteriorMenuStoredChoice choice = useConfirmedChoice
            ? confirmedVariantId is null
                ? InteriorMenuStoredChoice.Base()
                : catalog.TryGet(confirmedVariantId, out RuntimeInterior confirmed)
                    ? InteriorMenuStoredChoice.Custom(
                        confirmed.Definition.Id,
                        confirmed.Definition.ContentHash)
                    : InteriorMenuStoredChoice.Custom(
                        VariantId.Parse(confirmedVariantId),
                        ContentHash.Parse(new string('0', 64)))
            : ToMenuChoice(stored);

        view = InteriorMenuStateBuilder.Build(
            SelectedBuilding.Target,
            catalog.Entries.Select(entry => new InteriorMenuVariant(
                entry.Definition.Id,
                entry.Definition.DisplayName,
                entry.Definition.Target,
                entry.Definition.ContentHash,
                entry.SourcePackId,
                entry.SourcePackVersion,
                entry.PreviewAssetKey)),
            choice);
        selectedVariantIndex = Math.Clamp(selectedVariantIndex, 0, view.Options.Count - 1);
        LoadSelectedPreview();
    }

    private InteriorMenuStoredChoice ToMenuChoice(SelectionReadResult stored)
    {
        if (!stored.IsValid)
        {
            return InteriorMenuStoredChoice.Invalid(
                stored.Error ?? helper.Translation.Get("menu.warning.unknown-data"));
        }

        return stored.Selection.Choice switch
        {
            InteriorChoice.VanillaChoice => InteriorMenuStoredChoice.Base(),
            InteriorChoice.CustomChoice custom => InteriorMenuStoredChoice.Custom(
                custom.VariantId,
                custom.ContentHash),
            _ => InteriorMenuStoredChoice.Invalid(
                helper.Translation.Get("menu.warning.unknown-data")),
        };
    }

    private void SelectBuilding(int index)
    {
        if (index < 0 || index >= buildings.Count)
        {
            return;
        }

        selectedBuildingIndex = index;
        selectedVariantIndex = 0;
        variantScroll = 0;
        EnsureBuildingVisible();
        feedback = InteriorMenuFeedback.None;
        feedbackMessage = string.Empty;
        RefreshView();
        int current = view.Options.ToList().FindIndex(option => option.IsCurrent);
        selectedVariantIndex = current >= 0 ? current : 0;
        EnsureVariantVisible();
        RefreshClickableComponents();
    }

    private void SelectVariant(int index)
    {
        if (index < 0 || index >= view.Options.Count)
        {
            return;
        }

        selectedVariantIndex = index;
        feedback = InteriorMenuFeedback.None;
        feedbackMessage = string.Empty;
        EnsureVariantVisible();
        LoadSelectedPreview();
        RefreshClickableComponents();
    }

    private void ApplySelection()
    {
        if (IsPending)
        {
            ShowPendingBlocked();
            return;
        }

        Game1.playSound("smallSelect");
        mod.RequestMenuSelection(
            this,
            SelectedBuilding.Building,
            SelectedBuilding.Target,
            SelectedOption.VariantId);
    }

    private void MoveFocus(int delta)
    {
        focus = (InteriorMenuFocus)(((int)focus + delta + 3) % 3);
        SnapToFocus();
    }

    private void MoveSelection(int delta)
    {
        if (IsPending)
        {
            ShowPendingBlocked();
            return;
        }

        switch (focus)
        {
            case InteriorMenuFocus.Buildings:
                SelectBuilding(Math.Clamp(selectedBuildingIndex + delta, 0, buildings.Count - 1));
                break;
            case InteriorMenuFocus.Variants:
                int candidate = selectedVariantIndex + delta;
                if (candidate >= view.Options.Count)
                {
                    focus = InteriorMenuFocus.Apply;
                }
                else
                {
                    SelectVariant(Math.Max(0, candidate));
                }

                break;
            case InteriorMenuFocus.Apply when delta < 0:
                focus = InteriorMenuFocus.Variants;
                SelectVariant(view.Options.Count - 1);
                break;
        }

        SnapToFocus();
    }

    private void SnapToFocus()
    {
        int id = focus switch
        {
            InteriorMenuFocus.Buildings => BuildingComponentBase + selectedBuildingIndex,
            InteriorMenuFocus.Variants => VariantComponentBase + selectedVariantIndex,
            _ => ApplyComponentId,
        };
        setCurrentlySnappedComponentTo(id);
        snapCursorToCurrentSnappedComponent();
    }

    private void SyncFocusFromSnappedComponent()
    {
        if (currentlySnappedComponent is null)
        {
            return;
        }

        int id = currentlySnappedComponent.myID;
        focus = id switch
        {
            >= BuildingComponentBase and < VariantComponentBase => InteriorMenuFocus.Buildings,
            >= VariantComponentBase and < ApplyComponentId => InteriorMenuFocus.Variants,
            ApplyComponentId => InteriorMenuFocus.Apply,
            _ => focus,
        };
    }

    private void RefreshClickableComponents()
    {
        allClickableComponents = new List<ClickableComponent>();
        for (int visible = 0; visible < VisibleBuildingRows; visible++)
        {
            int index = buildingScroll + visible;
            if (index >= buildings.Count)
            {
                break;
            }

            var component = new ClickableComponent(
                new Rectangle(
                    buildingListBounds.X + 12,
                    buildingListBounds.Y + 36 + (visible * RowHeight),
                    buildingListBounds.Width - 24,
                    RowHeight - 6),
                $"building-{index}")
            {
                myID = BuildingComponentBase + index,
                upNeighborID = index > 0 ? BuildingComponentBase + index - 1 : ClickableComponent.SNAP_AUTOMATIC,
                downNeighborID = index + 1 < buildings.Count ? BuildingComponentBase + index + 1 : ApplyComponentId,
                rightNeighborID = VariantComponentBase + selectedVariantIndex,
            };
            allClickableComponents.Add(component);
        }

        for (int visible = 0; visible < VisibleVariantRows; visible++)
        {
            int index = variantScroll + visible;
            if (index >= view.Options.Count)
            {
                break;
            }

            var component = new ClickableComponent(
                new Rectangle(
                    variantListBounds.X + 12,
                    variantListBounds.Y + variantRowsTopInset + (visible * RowHeight),
                    variantListBounds.Width - 24,
                    RowHeight - 6),
                $"variant-{index}")
            {
                myID = VariantComponentBase + index,
                upNeighborID = index > 0 ? VariantComponentBase + index - 1 : ClickableComponent.SNAP_AUTOMATIC,
                downNeighborID = index + 1 < view.Options.Count ? VariantComponentBase + index + 1 : ApplyComponentId,
                leftNeighborID = BuildingComponentBase + selectedBuildingIndex,
                rightNeighborID = ApplyComponentId,
            };
            allClickableComponents.Add(component);
        }

        allClickableComponents.Add(new ClickableComponent(applyBounds, "apply")
        {
            myID = ApplyComponentId,
            upNeighborID = VariantComponentBase + selectedVariantIndex,
            leftNeighborID = VariantComponentBase + selectedVariantIndex,
        });

        if (upperRightCloseButton is not null)
        {
            allClickableComponents.Add(upperRightCloseButton);
        }
    }

    private void DrawPanel(SpriteBatch b, Rectangle bounds, string title)
    {
        drawTextureBox(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White * 0.96f);
        DrawText(b, title, new Vector2(bounds.X + 14, bounds.Y + 10), Game1.textColor, Game1.smallFont);
    }

    private void DrawBuildings(SpriteBatch b)
    {
        for (int visible = 0; visible < VisibleBuildingRows; visible++)
        {
            int index = buildingScroll + visible;
            if (index >= buildings.Count)
            {
                break;
            }

            Rectangle row = new(
                buildingListBounds.X + 12,
                buildingListBounds.Y + 36 + (visible * RowHeight),
                buildingListBounds.Width - 24,
                RowHeight - 6);
            bool selected = index == selectedBuildingIndex;
            bool hovered = hoveredComponentId == BuildingComponentBase + index;
            DrawRowBackground(b, row, selected, hovered);
            DrawText(
                b,
                Truncate(buildings[index].Label, Game1.smallFont, row.Width - 20),
                new Vector2(row.X + 10, row.Y + 7),
                Game1.textColor,
                Game1.smallFont);
            DrawText(
                b,
                Truncate(buildings[index].Detail, Game1.tinyFont, row.Width - 20),
                new Vector2(row.X + 10, row.Y + 36),
                Game1.textColor * 0.72f,
                Game1.tinyFont);
        }

        DrawScrollBar(b, buildingListBounds, buildings.Count, VisibleBuildingRows, buildingScroll, 36);
    }

    private void DrawVariants(SpriteBatch b)
    {
        string baseHelp = Game1.parseText(
            helper.Translation.Get("menu.base.description"),
            Game1.tinyFont,
            variantListBounds.Width - 28);
        DrawText(
            b,
            baseHelp,
            new Vector2(variantListBounds.X + 14, variantListBounds.Y + 38),
            Game1.textColor * 0.82f,
            Game1.tinyFont);

        for (int visible = 0; visible < VisibleVariantRows; visible++)
        {
            int index = variantScroll + visible;
            if (index >= view.Options.Count)
            {
                break;
            }

            InteriorMenuOption option = view.Options[index];
            Rectangle row = new(
                variantListBounds.X + 12,
                variantListBounds.Y + variantRowsTopInset + (visible * RowHeight),
                variantListBounds.Width - 24,
                RowHeight - 6);
            bool selected = index == selectedVariantIndex;
            bool hovered = hoveredComponentId == VariantComponentBase + index;
            DrawRowBackground(b, row, selected, hovered);

            string currentLabel = helper.Translation.Get("menu.current");
            int currentWidth = option.IsCurrent
                ? (int)Math.Ceiling(Game1.tinyFont.MeasureString(currentLabel).X) + 18
                : 0;
            string displayName = option.IsBase
                ? helper.Translation.Get("menu.base.name")
                : option.DisplayName;
            DrawText(
                b,
                Truncate(displayName, Game1.smallFont, row.Width - 22 - currentWidth),
                new Vector2(row.X + 10, row.Y + 8),
                Game1.textColor,
                Game1.smallFont);
            string source = option.IsBase
                ? helper.Translation.Get("menu.base.source")
                : helper.Translation.Get(
                    "menu.source",
                    new { pack = option.SourcePackId, version = option.SourcePackVersion });
            DrawText(
                b,
                Truncate(source, Game1.tinyFont, row.Width - 22),
                new Vector2(row.X + 10, row.Y + 36),
                Game1.textColor * 0.72f,
                Game1.tinyFont);

            if (option.IsCurrent)
            {
                DrawText(
                    b,
                    currentLabel,
                    new Vector2(row.Right - 10 - currentWidth + 8, row.Y + 10),
                    Color.DarkGreen,
                    Game1.tinyFont);
            }
        }

        DrawScrollBar(
            b,
            variantListBounds,
            view.Options.Count,
            VisibleVariantRows,
            variantScroll,
            variantRowsTopInset);
    }

    private void DrawPreview(SpriteBatch b)
    {
        int innerX = previewBounds.X + 16;
        int innerWidth = previewBounds.Width - 32;
        DrawText(
            b,
            Truncate(
                SelectedOption.IsBase
                    ? helper.Translation.Get("menu.base.name")
                    : SelectedOption.DisplayName,
                Game1.smallFont,
                innerWidth),
            new Vector2(innerX, previewBounds.Y + 42),
            Game1.textColor,
            Game1.smallFont);

        Rectangle imageBounds = new(
            innerX,
            previewBounds.Y + 80,
            innerWidth,
            Math.Max(120, Math.Min(250, previewBounds.Height - 236)));
        b.Draw(Game1.staminaRect, imageBounds, Color.Black * 0.12f);

        if (previewTexture is not null)
        {
            Rectangle destination = Fit(previewTexture.Bounds, imageBounds);
            b.Draw(previewTexture, destination, Color.White);
        }
        else
        {
            Color line = Color.SaddleBrown * 0.35f;
            b.Draw(Game1.staminaRect, new Rectangle(imageBounds.X + 12, imageBounds.Center.Y - 2, imageBounds.Width - 24, 4), line);
            b.Draw(Game1.staminaRect, new Rectangle(imageBounds.Center.X - 2, imageBounds.Y + 12, 4, imageBounds.Height - 24), line);
        }

        string placeholder = SelectedOption.IsBase
            ? helper.Translation.Get("menu.preview.base")
            : previewFailed
                ? helper.Translation.Get("menu.preview.failed")
                : previewTexture is null
                    ? helper.Translation.Get("menu.preview.none")
                    : string.Empty;
        int textY = imageBounds.Bottom + 14;
        if (placeholder.Length > 0)
        {
            DrawText(
                b,
                Game1.parseText(placeholder, Game1.tinyFont, innerWidth),
                new Vector2(innerX, textY),
                previewFailed ? Color.DarkRed : Game1.textColor * 0.76f,
                Game1.tinyFont);
        }

        string warning = FormatWarning(view.Warning);
        if (warning.Length > 0)
        {
            DrawText(
                b,
                Game1.parseText(warning, Game1.tinyFont, innerWidth),
                new Vector2(innerX, previewBounds.Bottom - 112),
                Color.DarkRed,
                Game1.tinyFont);
        }
    }

    private void DrawStatusAndApply(SpriteBatch b)
    {
        if (feedback != InteriorMenuFeedback.None)
        {
            Color statusColor = feedback switch
            {
                InteriorMenuFeedback.Applied => Color.DarkGreen,
                InteriorMenuFeedback.Pending => Color.DarkGoldenrod,
                _ => Color.DarkRed,
            };
            DrawText(
                b,
                Game1.parseText(feedbackMessage, Game1.smallFont, statusBounds.Width),
                new Vector2(statusBounds.X, statusBounds.Y + 6),
                statusColor,
                Game1.smallFont);
        }

        bool current = SelectedOption.IsCurrent;
        Color buttonColor = IsPending ? Color.Gray : Color.White;
        drawTextureBox(
            b,
            applyBounds.X,
            applyBounds.Y,
            applyBounds.Width,
            applyBounds.Height,
            buttonColor);
        string label = current && !IsPending
            ? helper.Translation.Get("menu.apply.current")
            : helper.Translation.Get("menu.apply");
        Vector2 size = Game1.smallFont.MeasureString(label);
        DrawText(
            b,
            label,
            new Vector2(
                applyBounds.Center.X - (size.X / 2),
                applyBounds.Center.Y - (size.Y / 2)),
            IsPending ? Color.DimGray : Game1.textColor,
            Game1.smallFont);
    }

    private string FormatWarning(InteriorMenuWarning warning) => warning.Kind switch
    {
        InteriorMenuWarningKind.InvalidStoredSelection => helper.Translation.Get(
            "menu.warning.invalid",
            new
            {
                detail = warning.Detail
                    ?? helper.Translation.Get("menu.warning.unknown-data"),
            }),
        InteriorMenuWarningKind.MissingVariant => helper.Translation.Get(
            "menu.warning.missing",
            new
            {
                variant = warning.VariantId
                    ?? helper.Translation.Get("menu.warning.unknown-variant"),
            }),
        InteriorMenuWarningKind.ContentHashMismatch => helper.Translation.Get(
            "menu.warning.hash",
            new
            {
                variant = warning.VariantId
                    ?? helper.Translation.Get("menu.warning.unknown-variant"),
            }),
        _ => string.Empty,
    };

    private void LoadSelectedPreview()
    {
        string? key = SelectedOption.PreviewAssetKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            previewTexture = null;
            previewFailed = false;
            loadedPreviewKey = null;
            return;
        }

        if (string.Equals(loadedPreviewKey, key, StringComparison.OrdinalIgnoreCase)
            && (previewTexture is not null || previewFailed))
        {
            return;
        }

        previewTexture = null;
        previewFailed = false;
        loadedPreviewKey = key;
        try
        {
            previewTexture = helper.GameContent.Load<Texture2D>(key);
        }
        catch (Exception exception)
        {
            previewFailed = true;
            monitor.Log(
                $"Couldn't load optional interior preview '{key}'. The variant remains usable. {exception.Message}",
                LogLevel.Warn);
        }
    }

    private string FormatBuildingLabel(InteriorTarget target) => helper.Translation.Get(
        target == InteriorTarget.Greenhouse
            ? "menu.building.greenhouse"
            : "menu.building.barn");

    private string FormatBuildingDetail(Building building, InteriorTarget target)
    {
        string id = building.id.Value.ToString("N")[..8];
        return helper.Translation.Get(
            target == InteriorTarget.Greenhouse
                ? "menu.building.detail.greenhouse"
                : "menu.building.detail.barn",
            new { x = building.tileX.Value, y = building.tileY.Value, id });
    }

    private static void DrawText(
        SpriteBatch b,
        string text,
        Vector2 position,
        Color color,
        SpriteFont font) => b.DrawString(font, text, position, color);

    private static void DrawRowBackground(
        SpriteBatch b,
        Rectangle bounds,
        bool selected,
        bool hovered)
    {
        Color color = selected
            ? Color.Goldenrod * 0.34f
            : hovered
                ? Color.SandyBrown * 0.22f
                : Color.Black * 0.06f;
        b.Draw(Game1.staminaRect, bounds, color);
    }

    private static void DrawScrollBar(
        SpriteBatch b,
        Rectangle panel,
        int itemCount,
        int visibleCount,
        int scroll,
        int topInset)
    {
        if (itemCount <= visibleCount)
        {
            return;
        }

        Rectangle track = new(panel.Right - 7, panel.Y + topInset, 3, panel.Height - topInset - 10);
        b.Draw(Game1.staminaRect, track, Color.Black * 0.18f);
        int thumbHeight = Math.Max(20, track.Height * visibleCount / itemCount);
        int travel = track.Height - thumbHeight;
        int maxScroll = itemCount - visibleCount;
        int thumbY = track.Y + (travel * scroll / maxScroll);
        b.Draw(Game1.staminaRect, new Rectangle(track.X - 2, thumbY, 7, thumbHeight), Color.SaddleBrown * 0.7f);
    }

    private static Rectangle Fit(Rectangle source, Rectangle bounds)
    {
        float scale = Math.Min(
            bounds.Width / (float)source.Width,
            bounds.Height / (float)source.Height);
        int width = Math.Max(1, (int)(source.Width * scale));
        int height = Math.Max(1, (int)(source.Height * scale));
        return new Rectangle(
            bounds.Center.X - (width / 2),
            bounds.Center.Y - (height / 2),
            width,
            height);
    }

    private static string Truncate(string text, SpriteFont font, int maxWidth)
    {
        if (font.MeasureString(text).X <= maxWidth)
        {
            return text;
        }

        const string Ellipsis = "…";
        int length = text.Length;
        while (length > 0 && font.MeasureString(text[..length] + Ellipsis).X > maxWidth)
        {
            length--;
        }

        return length == 0 ? Ellipsis : text[..length] + Ellipsis;
    }

    private static int Scroll(int current, int direction, int count, int visible)
    {
        int delta = direction > 0 ? -1 : 1;
        return Math.Clamp(current + delta, 0, Math.Max(0, count - visible));
    }

    private void ClampScrolls()
    {
        buildingScroll = Math.Clamp(buildingScroll, 0, Math.Max(0, buildings.Count - VisibleBuildingRows));
        variantScroll = Math.Clamp(variantScroll, 0, Math.Max(0, view.Options.Count - VisibleVariantRows));
        EnsureBuildingVisible();
        EnsureVariantVisible();
    }

    private void EnsureBuildingVisible()
    {
        if (selectedBuildingIndex < buildingScroll)
        {
            buildingScroll = selectedBuildingIndex;
        }
        else if (selectedBuildingIndex >= buildingScroll + VisibleBuildingRows)
        {
            buildingScroll = selectedBuildingIndex - VisibleBuildingRows + 1;
        }
    }

    private void EnsureVariantVisible()
    {
        if (selectedVariantIndex < variantScroll)
        {
            variantScroll = selectedVariantIndex;
        }
        else if (selectedVariantIndex >= variantScroll + VisibleVariantRows)
        {
            variantScroll = selectedVariantIndex - VisibleVariantRows + 1;
        }
    }
}
