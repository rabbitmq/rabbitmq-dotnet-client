import Core

info = {
    "friendly_name": "Block-quoted text",
    "summary": "Prints child paragraphs as a blockquote.",
}

def SublanguageHandler(args, doc, renderer):
    renderer.push_visit_pop(Core.Container('quote'), doc.children)
