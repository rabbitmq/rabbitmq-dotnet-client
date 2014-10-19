import Inline

info = {
    "friendly_name": "Link (External)",
    "example_template": "scheme://authority/path?query|Text To Display",
    "summary": "A more flexible way of linking to an external resource.",
    "details": """

    <p>Links to external resources can be embedded in the page by
    <i>naked linking</i>, just mentioning the URL itself, or by using
    this plugin. The text within the span is split at the first"|" character.
    The first part is interpreted as the URL to link to, and
    the second part is used as the display text for the link.</p>

    <p>For example, [link http://www.google.com|Google] is rendered to
    &lt;a href="http://www.google.com"&gt;Google&lt;/a&gt;.</p>

    """
}

def SpanHandler(rest, acc):
    (text, rest) = Inline.collectSpan(rest)
    textparts = text.split('|', 1)
    if len(textparts) > 1:
        target = textparts[0]
        vistext = textparts[1]
    else:
        target = text
        vistext = target
    acc.append(Inline.ExternalLink(target, vistext))
    return rest
