import json
from pprint import pprint
import os
import requests

category = "Urban"

def F1():
    json_text = ""
    with open("text.json", "r") as file:
        json_text = file.read()

    data = json.loads(json_text)

    links = [[i["download"], i["title"]] for i in data["items"]]

    os.makedirs(category, exist_ok=False)

    for [link, title] in links:
        title = title.replace(" ", "_")
        L = link.replace("wix:document://v1/archives/", "")
        L = L.split("/")[0]
        url = "https://www.thebasemesh.com/_files/archives/" + L
        print(url, title)

        # Download the file and save to category folder
        r = requests.get(url)
        with open(f"{category}/{title}.zip", "wb") as file:
            file.write(r.content)

def F2():
    json_text = ""
    with open("text.json", "r") as file:
        json_text = file.read()

    data = json.loads(json_text)

    links = [[i["data"]["download"], i["data"]["title"]] for i in data["dataItems"]]

    os.makedirs(category, exist_ok=True)
    i = 0

    for [link, title] in links:
        title = title.replace(" ", "_")
        L = link.replace("wix:document://v1/archives/", "")
        L = L.split("/")[0]
        url = "https://www.thebasemesh.com/_files/archives/" + L
        print(i, len(links), url, title)
        i+=1

        # Download the file and save to category folder
        r = requests.get(url)
        with open(f"{category}/{title}.zip", "wb") as file:
            file.write(r.content)

F2()