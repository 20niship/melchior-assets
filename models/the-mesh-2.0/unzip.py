
import os
import glob
import shutil

def unzip():
    folder = "Urban"

    files = glob.glob(f"{folder}/*.zip")

    os.makedirs("tmp", exist_ok=True)

    for f in files:
        filename = f.split("/")[-1].split(".")[0]
        os.makedirs(filename, exist_ok=True)
        shutil.unpack_archive(f, filename)
        os.remove(f)


def glb_extract():
    files = os.listdir(".")
    files = ["Urban"]
    for f in files:
        print(f)
        meshes = glob.glob(f"{f}/*/*.glb")
        for m in meshes:
            filename = os.path.basename(m)
            new_name = "./" + f + "/" + filename
            print(new_name)
            os.rename(m, new_name)
    
glb_extract()

# unzip()