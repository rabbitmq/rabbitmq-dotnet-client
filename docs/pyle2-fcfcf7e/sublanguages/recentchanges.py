import Core

info = {
    "friendly_name": "Recent Changes List",
    "example_template": "changecount",
    "summary": "Inserts a description of recent Wiki activity.",
    "details": """

    <p>If 'changecount' is omitted, all changes recorded since the
    current server was started are printed; otherwise, the list is
    limited to just the most recent 'changecount' changes.</p>

    """
}

def SublanguageHandler(args, doc, renderer):
    if args.strip():
        count = int(args.strip())
    else:
        count = None
    renderer.add(Core.RecentChanges(count))
