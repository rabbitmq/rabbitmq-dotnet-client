import Core

info = {
    "friendly_name": "Marginal text",
    "summary": "Prints child paragraphs in a div of class 'sidebar'.",
}

def SublanguageHandler(args, doc, renderer):
    renderer.push_visit_pop(Core.Container('sidebar'), doc.children)
