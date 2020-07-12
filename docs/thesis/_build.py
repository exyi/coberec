from jinja2 import Environment, FileSystemLoader
import subprocess
import os
import re

def get_git_root() -> str:
    return subprocess.check_output("git rev-parse --show-toplevel".split(" ")).decode('utf8')[:-1]

def convert_external_links_to_footnotes(text) -> str:
    return re.sub(r"([^!])\[(.*?)\]\(([^#].*?)\)", r"\1[\2](\3)^[[\3](\3)]", text)

# to make the runtime environment consistent, cd to thesis directory
os.chdir(get_git_root())
os.chdir('docs/thesis')

def get_git_commit() -> str:
    return subprocess.check_output("git rev-parse --short HEAD".split(" ")).decode('utf8')[:-1]

def add_git_commit(text: str) -> str:
    return re.sub(r"%commitid%", get_git_commit(), text)

def remove_languages(text: str) -> str:
    return re.sub(r"```.*", "```", text)

# generate latex files
for f in os.listdir():
    if not f.endswith(".md"):
        continue
    with open(f, 'r') as q:
        text = add_git_commit(q.read())
        text = remove_languages(text)
        text = convert_external_links_to_footnotes(text)
        subprocess.run(f"pandoc --top-level-division=chapter -f markdown+smart -t latex -o {f}.tex", shell=True, input=text, encoding='utf8')


# Render using only pandoc
#env = Environment(loader=FileSystemLoader('.'))
#template = env.get_template('thesis.md.j2')
#text = template.render()
#text = convert_external_links_to_footnotes(text)

#subprocess.run("dot -Tpdf res/restart_procedure_diagram.dot > res/restart_procedure_diagram.pdf", shell=True)
#subprocess.run("pandoc -f markdown+smart -o thesis.pdf", shell=True, input=text, encoding='utf8')
#subprocess.run("pandoc -f markdown+smart -t html5 -o thesis.html", shell=True, input=text, encoding='utf8')
