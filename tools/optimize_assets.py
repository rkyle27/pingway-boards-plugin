# Usage: python3 -I tools/optimize_assets.py <raw output folder> PingwayBoards/Assets   (needs Pillow)
# Crops the blank posters to a fixed margin and packs every PNG into a palette PNG.
# Boards (opaque, big smooth gradients): 255 median-cut colours + dithering, the rounded corners = one transparent colour.
# Posters / sign (soft shadows): 256-colour palette with alpha.
import os, sys
from PIL import Image
SRC, DST = sys.argv[1], sys.argv[2]
os.makedirs(DST, exist_ok=True)
for f in sorted(os.listdir(SRC)):
    im = Image.open(os.path.join(SRC, f)).convert('RGBA')
    if f.startswith('board_'):
        alpha = im.getchannel('A')
        rgb = Image.new('RGB', im.size, (0, 0, 0))
        rgb.paste(im.convert('RGB'), mask=alpha)
        q = rgb.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG)
        pal = q.getpalette()[:255 * 3] + [0, 0, 0]
        q.putpalette(pal)
        px = q.load(); a = alpha.load()
        for y in range(im.height):
            for x in range(im.width):
                if a[x, y] < 128:
                    px[x, y] = 255
        q.info['transparency'] = 255
        q.save(os.path.join(DST, f), optimize=True, transparency=255)
    else:
        if f.startswith('poster_'):
            im = im.crop((16, 12, 324, 454))  # card at 14,18 .. 294,418 inside a 308x442 image
        q = im.quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.FLOYDSTEINBERG)
        q.save(os.path.join(DST, f), optimize=True)
    print(f, im.size, os.path.getsize(os.path.join(DST, f)))
