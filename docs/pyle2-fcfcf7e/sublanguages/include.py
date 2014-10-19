import Core
import re

info = {
    "friendly_name": "Include Page",
    "example_spacing": " PageTitle",
    "example_template": "",
    "summary": "Transcludes the contents of another page into the current page.",
    "details": """

    <p>If the included page has section (sub-)headings, they will be
    included as (sub-)subheadings of the section in which the @include
    block appears, unless the word 'toplevel' is placed after the page
    title, like this:</p>

    <pre>
    @include PageTitle toplevel
    </pre>

    """
}

def SublanguageHandler(args, doc, renderer):
    if doc.paragraph.indent > 0:
        raise Core.BlockSyntaxError("'@include' must not be indented at all")

    args = [x.strip() for x in re.split(' *', args)]
    if not args or not args[0]:
        raise Core.BlockSyntaxError("'@include' needs a PageTitle")
    
    pagename = args[0]
    toplevel = (len(args) > 1) and args[1] == 'toplevel'
    
    page = Core.Page(pagename)

    if toplevel:
        page.render_on(renderer)
    else:
        old_rank_offset = renderer.save_rank_offset()
        try:
            page.render_on(renderer)
        finally:
            renderer.restore_rank_offset(old_rank_offset)

    if page.exists():
        renderer.page.mark_dependency_on(pagename)
