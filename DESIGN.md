# MiniDeck visual system

Stack: Windows Forms / GDI+. Movable dial with persisted size (280-560px; default 360px) and position.

Applied ui-ux-pro-max: `dark productivity interface --domain style` matched Dark Mode (OLED).
The initial design-system search returned a web landing-page pattern, which was not applied to this desktop utility.
The user subsequently requested a light dial: use warm ivory (#F7F7F2), sage selection, darker readable text, larger app icons and no orbit lines or peripheral labels. Settings remain dark. Full app names live in a separate wrapped caption, with scrolling for long names and no ellipsis. This explicit preference overrides prior dark-dial recommendations.

Tokens: background #12151C, surface #1C212B, border #434E61, text #EFF3FA, secondary #B1BCCF, accent #A0BBFF.
Typography: bundled Pretendard for Korean/mixed names and Inter for Latin names, registered privately in the process. No system font installation. Licenses bundled with fonts.

Glass refinement: Windows layered window with UpdateLayeredWindow and premultiplied per-pixel alpha. Render at 2x and downsample; do not use a binary circular window region. Light translucent surface, dual specular rim, clipped highlight and soft shadow. This is custom glass styling, without live backdrop blur or optical refraction. Render the caption in the same alpha surface to avoid jagged native-control boundaries. Preserve caption wrapping and scrolling.
Interaction: selection changes use existing short easing, respecting Windows menu animation preference. Drag/click separation and persisted location remain intact.
Custom icons: fit inside a transparent square, never stretch; selected icon grows with its target. PNG and self-contained SVG are imported to managed storage. No external SVG resources are loaded.

Scope: native desktop only. Phone safe-area checks and web page guidance are not applicable. Native settings are keyboard accessible; the custom dial remains operated through global shortcuts and mouse, not a complete screen-reader navigation surface.
