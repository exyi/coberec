from jinja2 import Environment, FileSystemLoader
import subprocess
import os
import re

citMap = {




    "https://en.wikipedia.org/wiki/Boilerplate_code": "wiki:Boilerplate",
    "https://en.wikipedia.org/wiki/Symbol_%28programming%29": "wiki:Symbol",
    "https://en.wikipedia.org/wiki/Common_Intermediate_Language": "wiki:CIL",
    "https://docs.microsoft.com/en-us/dotnet/framework/reflection-and-codedom/reflection": "SystemReflection",
    "https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/": "CsharpSourceGen",
    "https://github.com/kevin-montrose/Jil#optimizing-member-access-order": "Jil",
    "https://github.com/kevin-montrose/Jil": "Jil",
    "https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression?view=netframework-4.7.2": "SystemLinqExpressions",
    "https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.assemblybuilder?view=netcore-3.1": "AssemblyBuilder",
    "https://fsharp.github.io/FSharp.Data/library/JsonProvider.html": "FSharpJson",
    "https://github.com/fsprojects/OpenAPITypeProvider": "FSharpOpenAPI",
    "https://fsprojects.github.io/SQLProvider/": "FSharpSql",
    "https://github.com/demetrixbio/FSharp.Data.Npgsql": "FSharpDataNpgsql",
    "https://www.scalatest.org/user_guide/using_assertions": "ScalaTestAssert",
    "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy": "JSProxy",
    "https://medium.com/dailyjs/how-to-use-javascript-proxies-for-fun-and-profit-365579d4a9f8": "JSProxyHacks",
    "https://www.newtonsoft.com/json/help/html/QueryJsonDynamic.htm": "NJsonDynamic",
    "https://github.com/Fody/Fody/tree/c31ea96b5be6ce66b992614dda2af2c0a9bb91d2#edit-and-continue": "FodyEditAndContinue",
    "https://doc.postsharp.net/requirements#incompatibilities": "PostSharpEditAndContinue",
    "https://www.w3.org/TR/wasm-core-1/#control-instructions%E2%91%A8": "WasmControl",
    "https://github.com/icsharpcode/ILSpy/blob/master/doc/ILAst.txt": "ILAst",
    "https://docs.microsoft.com/en-us/dotnet/api/system.reflection.metadata?view=netcore-3.1": "SystemReflectionMetadata",
    "https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/lambda-expressions": "LambdaFunctions",
    "https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/local-functions": "LocalFunctions",
    "https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/interfaces/explicit-interface-implementation": "ExplicitInterfaceImpl",
    "https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.blockexpression?view=netcore-3.1": "BlockExpression",
    "https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/auto-implemented-properties": "AutoProperties",
    "https://blog.ploeh.dk/2020/06/29/syntactic-sugar-for-io/": "AutoProperties",
    "https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/from-clause": "LinqFromClause",
    "https://graphql.org/learn/schema/#union-types": "GraphqlUnion",
    "https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression": "SwitchExpression",
    "https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/caller-information": "CallerInfo",
    "https://github.com/fscheck/FsCheck": "FSCheck",
    "https://github.com/minimaxir/big-list-of-naughty-strings/": "BLNS",
    "https://dlang.org/articles/mixin.html": "Dmixin",
    "https://github.com/mono/linker/blob/master/docs/illink-tasks.md": "ILLinker",
    "https://github.com/JamesNK/Newtonsoft.Json": "NJson",
    "https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-3.1": "AspNetDI",
    "https://github.com/StackExchange/Dapper": "Dapper",
    "https://github.com/dotnet/efcore": "EFCore",
    "https://github.com/khellang/Scrutor": "Scrutor",
    "https://automapper.org/": "AutoMapper",
    "https://github.com/scalalandio/chimney": "Chimney",
    "https://automapper.org/":"AutoMapper",
    "https://github.com/Fody/Equals":"FodyEquals",
    "https://github.com/Fody/PropertyChanged":"FodyPropertyChanged",
    "https://github.com/csnemes/tracer":"FodyTracer",
    "https://github.com/Fody/MethodTimer":"FodyMethodTimer",
    "https://github.com/Fody/Caseless":"FodyCaseless",
    "https://github.com/wazowsk1/LoggerIsEnabled.Fody":"FodyLoggerIsEnabled",
    "https://github.com/Fody/Fody":"Fody",
    "https://mikhail.io/2016/05/tweaking-immutable-objects-with-csharp-and-fody/":"FodyWithBlog",
    "https://github.com/icsharpcode/ILSpy": "ILSpy",
    "https://github.com/reactiveui/refit": "Refit",
}

footnoteLinkBlacklist = [
    "https://github.com/exyi/coberec/blob/master/src/Coberec.Tests/CSharp/testoutputs/CodeGeneratorTests.ThesisExample.cs",
    "https://github.com/exyi/coberec/blob/master/docs/graphql-gen.md",
    "https://github.com/exyi/coberec/blob/master/src/Coberec.Tests/CSharp/testoutputs/CodeGeneratorTests.SimpleUnionType.cs"
]

def get_git_root() -> str:
    return subprocess.check_output("git rev-parse --show-toplevel".split(" ")).decode('utf8')[:-1]

def convert_external_links_to_footnotes(text) -> str:
    def match(x):
        if x.group(3) in footnoteLinkBlacklist:
            return x.group(0)
        return f"{x.group(1)}[{x.group(2)}]({x.group(3)})^[[{x.group(3)}]({x.group(3)})]"
    return re.sub(
        r"([^!])\[(.*?)\]\(([^#].*?)\)",
        match,
        text
    )

def convert_internal_links(text: str) -> str:
    def matchFn(m):
        linkText = m.group(2)
        file = m.group(3)
        header = m.group(5)
        if header is None:
            header = {
                "intro.md": "introduction",
                "approaches.md": "approaches-to-reducing-boilerplate-code",
                "design.md": "design",
                "internals.md": "implementation-of-the-api",
                "API-overview.md": "api-overview",
                "graphql-generator.md": "c-from-graphql-schema-generator",
                "conclusion.md": "conclusion"
            }[file]
            return f"{m.group(1)}{linkText} (chapter \\ref{{{header}}})"
        else:
            return f"{m.group(1)}{linkText} (\\ref{{{header}}})"
    return re.sub(r"([^!]|^)\[(.*?)\]\(\.\/(.*?)(\#(.*?))?\)", matchFn, text)

def convert_svg_links(text: str) -> str:
    def matchFn(m):
        linkText = m.group(2)
        file = m.group(3)
        fileCore, ext = os.path.splitext(file)
        return f"![{linkText}]({fileCore}.pdf)"
#         return f"""
# \\begin{{figure}}
#     \\centering
#     \\def\\svgwidth{{\\columnwidth}}
#     \\scalebox{{0.5}}{{\\input{{{fileCore}.pdf_tex}}}}
# \\end{{figure}}"""
    return re.sub(r"(\!)\[(.*?)\]\(\.\/(.*?\.svg)\)", matchFn, text)

def convert_links_to_citations(text: str) -> str:
    def matchFn(m):
        linkText = m.group(2)
        link = m.group(3)
        if link in citMap:
            return f"{m.group(1)}{linkText} \cite{{{citMap[link]}}}"
        else:
            return m.group(0)
    return re.sub(r"([^!]|^)\[(.*?)\]\((http.*?)\)", matchFn, text)


def remove_next_links(text: str) -> str:
    return re.sub(r"\bNext:.*", r"", text)

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
        text = convert_links_to_citations(text)
        text = convert_internal_links(text)
        text = remove_next_links(text)
        text = convert_svg_links(text)
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
