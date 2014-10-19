import Inline

info = {
    "friendly_name": "HTML (Block)",
    "example_template": "htmltext",
    "summary": "Inserts a larger block of literal HTML into the rendered output.",
}

def SublanguageHandler(args, doc, renderer):
    renderer.add(Inline.HtmlFragment(doc.reconstruct_child_text().as_string()))
