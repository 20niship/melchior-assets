from PIL import Image
import sys

for fname in sys.argv[1:]:
    image = Image.open(fname)
    resized_image = image.resize((512, 512))
    resized_image.save(fname)

print("画像が正常にリサイズされました。")

