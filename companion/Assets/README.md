# Realmly Companion icon

Source: realmly-compass-v1.png, generated with the built-in image-generation tool
using the existing addon compass emblem as a reference. Transparent alpha is
preserved. The addon artwork itself has not been changed.

Final prompt:

> Use case: logo-brand. Asset type: polished Windows application icon for Realmly
> Companion, transparent square PNG. Image 1 is the existing Realmly compass
> emblem reference. Re-render this emblem in high resolution, preserving its
> recognizable round gold compass, teal map face, bronze rim and bold four-point
> gold compass rose. Clean front-facing symmetrical composition, hand-painted
> fantasy RPG UI item icon style with warm beveled gold and bronze, deep teal
> enamel, restrained engraved map details, bright edges and strong dark
> separations readable at 16-32 pixels. Center the emblem and fill about 90% of
> the square. Crisp intentional silhouette, subtle dimensional shading, no text,
> no letters, no background tile, no scenic backdrop, no extra props, no watermark.
> Real transparent alpha surrounding the circular icon.

Rebuild the Windows ICO with ../tools/BuildIcon.ps1. It packages transparent PNG
frames at 16, 24, 32, 48, 64, 128 and 256 pixels. The ICO is both the executable
application icon and an embedded resource for the window/taskbar icon.
