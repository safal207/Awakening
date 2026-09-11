# Material provenance

## brick-albedo.png

- Original AI-generated raster material, created for Awakening on 2026-09-10.
- Tool: built-in image generation, text-to-image mode, no reference image.
- Saved asset: `assets/materials/brick-albedo.png`.
- Actual size: 1254 x 1254 pixels; the requested size was 1024 x 1024.
- Use: sRGB base color, repeating world-space coordinates, generated mipmaps.
- No film frame, actor likeness, third-party photograph or game texture was used.
- This is an albedo image, not a full PBR material set. There are no supplied
  normal, height or measured roughness maps. Repeating edges are not mathematically
  guaranteed seamless and may need dedicated texture-authoring cleanup.
- Distributed with the project under its MIT license, to the extent applicable.

Generation prompt (verbatim):

> Create one production-ready seamless square tileable PBR BASE COLOR texture of authentic weathered New York red-brown brick masonry with narrow pale gray mortar joints, running bond pattern. Asset for a real-time game, not a scene illustration. Orthographic straight-on flat surface with NO perspective, NO cast shadows, NO directional light, NO vignette, no border, no text, no logos, no objects. Uniform neutral diffuse lighting. About 12 horizontal courses of standard rectangular bricks across the full height, about 6 brick lengths across full width, staggered alternating rows. Naturally varied desaturated warm red and russet and a few dark charcoal bricks, very fine believable porous texture and mild urban weathering, clean legible mortar lines, no exaggerated cracks or strong stains. Equal brightness on all edges and seamless repeat left-right top-bottom. Fill entire image with masonry. Output square 1024x1024 texture for use as albedo.

The prompt is a generation request, not a guarantee of physical accuracy.
PNG decoding uses StbImageSharp 2.30.16, a public-domain/Unlicense dependency:
https://github.com/StbSharp/StbImageSharp
