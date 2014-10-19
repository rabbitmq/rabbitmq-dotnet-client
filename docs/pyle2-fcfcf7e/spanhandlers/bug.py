import Config
import Inline

info = {
    "friendly_name": "Bugzilla Bug Link",
    "example_template": "12345",
    "summary": "Links to the given Bugzilla bug report record number.",
    "details": """

    <p>Uses the <code>Config.bug_url_template</code> URL,
    <tt>%s</tt>,
    to provide a link to the Bugzilla bug report with the number given.</p>

    """ % Config.bug_url_template
}

def SpanHandler(rest, acc):
    (text, rest) = Inline.collectSpan(rest)
    text = text.strip()
    acc.append(Inline.ExternalLink(Config.bug_url_template % text, "bug %s" % text))
    return rest
