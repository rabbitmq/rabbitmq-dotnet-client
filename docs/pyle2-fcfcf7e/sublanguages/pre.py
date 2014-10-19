import Inline

info = {
    "friendly_name": "Preformatted text (Block)",
    "summary": "Prints child paragraphs in a monospace typeface, while still interpreting them as markup.",
}

def SublanguageHandler(args, doc, renderer):
    text = doc.reconstruct_child_text().as_string()
    (fragments, rest) = Inline.parse(text)
    renderer.add(Inline.TagFragment('pyle_pre', fragments, 'pre'))
