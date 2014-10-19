import Inline

info = {
    "friendly_name": "Preformatted text (Inline)",
    "summary": "Prints the contained text in a monospace typeface, while still interpreting it as markup.",
}

def SpanHandler(rest, acc):
    (fragments, rest) = Inline.parse(rest)
    acc.append(Inline.TagFragment('pyle_tt', fragments))
    return rest
