import Inline

info = {
    "friendly_name": "Literal text",
    "example_template": "literaltext",
    "summary": "Passes the contained text directly to the output, without interpreting it as markup.",
    "details": """

    <p>The text is HTML-escaped, and close-brackets are counted (for the
    purposes of exiting from the <tt>literal</tt> span!), but no
    markup rendering is performed.</p>

    """
}

def SpanHandler(rest, acc):
    (text, rest) = Inline.collectSpan(rest)
    acc.append(Inline.LiteralFragment(text))
    return rest
