<frame layout={RootLayout} padding="16" border-thickness="12"
       background={@Mods/StardewUI/Sprites/MenuBackground} border={@Mods/StardewUI/Sprites/MenuBorder}>
    <lane layout="stretch content" orientation="vertical">
        <lane layout="stretch content" vertical-content-alignment="middle">
            <label layout="stretch content" font="dialogue" text={#menu.title} color="#3C4F2F" />
            <label text={InteriorCount} color="#77654B" scale="0.8" margin="0,0,80,0" />
        </lane>
        <label layout="stretch content" text={#menu.subtitle} color="#77654B" scale="0.85" margin="0,4,0,12" max-lines="1" />
        <lane layout="stretch content" vertical-content-alignment="middle" margin="0,0,0,16">
            <label text={#menu.buildings} margin="0,0,16,0" color="#5A4936" />
            <dropdown options={:BuildingLabels} selected-option={<>SelectedBuildingLabel}
                      selection-frame-layout={BuildingLayout} option-max-lines="1" max-list-height="280"
                      pointer-events-enabled={CanChoose} />
        </lane>
        <lane layout={BodyLayout} orientation={BodyOrientation}>
            <frame layout={ListLayout} padding="12" background={@Mods/StardewUI/Sprites/ControlBorder}>
                <lane layout="stretch" orientation="vertical">
                    <label text={#menu.variants} color="#5A4936" margin="4,0,0,10" />
                    <scrollable layout="stretch" peeking="48" drag-to-scroll="true">
                        <lane layout="stretch content" orientation="vertical">
                            <frame *repeat={Cards} layout="stretch content" padding="12" margin="0,0,0,8"
                                   background={@Mods/StardewUI/Sprites/MenuSlotInsetUncolored}
                                   background-tint={BackgroundTint} focusable="true" focusable-tag={Tag}
                                   tooltip={:Source} click=|^SelectVariant(Index)|
                                   +hover:opacity="0.85">
                                <lane layout="stretch content" orientation="vertical">
                                    <lane layout="stretch content" vertical-content-alignment="middle">
                                        <label layout="18px content" text={SelectionMark} color="#3C642C" />
                                        <label layout="stretch content" text={:Name} max-lines="2" />
                                    </lane>
                                    <label *if={IsCurrent} text={#menu.current} scale="0.7" color="#3C642C" margin="18,6,0,0" />
                                    <label layout="stretch content" text={:Source} scale="0.7" color="#77654B" max-lines="1" margin="18,6,0,0" />
                                </lane>
                            </frame>
                        </lane>
                    </scrollable>
                </lane>
            </frame>
            <frame layout="stretch" padding="16" margin={PreviewMargin} background={@Mods/StardewUI/Sprites/ControlBorder}>
                <scrollable layout="stretch" peeking="40">
                    <lane layout="stretch content" orientation="vertical">
                        <label layout="stretch content" text={SelectedName} color="#3C4F2F" max-lines="2" />
                        <label *if={SelectedIsCurrent} text={#menu.current} color="#3C642C" scale="0.75" margin="0,6,0,0" />
                        <frame layout={PreviewImageLayout} margin="0,12" padding="12"
                               background={@Mods/StardewUI/Sprites/MenuSlotInsetUncolored} background-tint="#D3CCA5"
                               horizontal-content-alignment="middle" vertical-content-alignment="middle">
                            <image *if={HasPreview} layout="stretch" sprite={Preview} fit="contain"
                                   horizontal-alignment="middle" vertical-alignment="middle" />
                            <lane *if={HasNoPreview} layout="stretch content" orientation="vertical" horizontal-content-alignment="middle">
                                <label text={#menu.preview} color="#756D4D" scale="0.8" margin="0,0,0,10" />
                                <label layout="stretch content" text={PreviewMessage} color="#756D4D" scale="0.85" max-lines="3" />
                            </lane>
                        </frame>
                        <label layout="stretch content" text={Description} color="#5A4936" scale="0.85" />
                        <label layout="stretch content" text={SelectedSource} color="#77654B" scale="0.75" margin="0,10,0,0" />
                        <frame *if={HasWarning} layout="stretch content" padding="10" margin="0,12,0,0"
                               background={@Mods/StardewUI/Sprites/MenuSlotInsetUncolored} background-tint="#F0CE9D">
                            <label layout="stretch content" text={Warning} color="#973223" scale="0.8" />
                        </frame>
                    </lane>
                </scrollable>
            </frame>
        </lane>
        <lane layout="stretch content" margin="0,16,0,0" vertical-content-alignment="middle">
            <label layout="stretch content" text={Status} color={StatusColor} scale="0.8" max-lines="3" margin="0,0,12,0" tooltip={Status} />
            <button text={ApplyLabel} default-background-tint={ApplyTint} hover-background-tint={ApplyTint}
                    pointer-events-enabled={CanApply} click=|ApplySelection()| />
        </lane>
    </lane>
</frame>
