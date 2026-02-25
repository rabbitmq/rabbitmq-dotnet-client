#!/usr/bin/env bash

# Generate a CHANGELOG.md section for a new release by querying GitHub
# for milestone issues/PRs and discovering non-milestoned PRs via git log.
#
# Usage: tools/generate-changelog.sh v7.2.0 v7.2.1

set -o errexit
set -o nounset
set -o pipefail

show_usage() {
    cat << EOF
Usage: $0 <previous-tag> <new-tag>

Generate a CHANGELOG.md section for a new release.

Arguments:
  previous-tag  The tag of the previous release (e.g. v7.2.0)
  new-tag       The tag of the new release (e.g. v7.2.1)

Both tags must include the 'v' prefix.

Examples:
  $0 v7.2.0 v7.2.1
  $0 v7.1.2 v7.2.0
EOF
}

if (( $# != 2 ))
then
    show_usage
    exit 1
fi

declare -r prev_tag="$1"
declare -r new_tag="$2"

if [[ "$prev_tag" != v* ]] || [[ "$new_tag" != v* ]]
then
    echo "Error: both tags must start with 'v'" >&2
    exit 1
fi

# Milestone name is the version without the v prefix
declare -r milestone="${new_tag#v}"

declare -r repo_url='https://github.com/rabbitmq/rabbitmq-dotnet-client'

# Collect all issue numbers and their categories.
# Key: issue number, Value: "bug", "enhancement", or "other"
declare -A issue_category
# Key: issue number, Value: title
declare -A issue_title
# Collect all PR data.
# Key: pr number, Value: title
declare -A pr_title
# Key: pr number, Value: author login
declare -A pr_author

add_issue() {
    local -ri num="$1"
    local -r title="$2"
    local -r category="$3"

    if [[ -z "${issue_category[$num]:-}" ]]
    then
        issue_category[$num]="$category"
        issue_title[$num]="$title"
    fi
}

categorize_labels() {
    local -r labels="$1"

    if echo "$labels" | jq -e 'map(.name) | index("bug")' > /dev/null 2>&1
    then
        echo 'bug'
    elif echo "$labels" | jq -e 'map(.name) | index("enhancement")' > /dev/null 2>&1
    then
        echo 'enhancement'
    else
        echo 'other'
    fi
}

echo "Fetching closed issues for milestone $milestone..." >&2

while IFS=$'\t' read -r num title labels
do
    category=$(categorize_labels "$labels")
    add_issue "$num" "$title" "$category"
done < <(gh issue list \
    --state closed \
    --milestone "$milestone" \
    --json number,title,labels \
    --jq '.[] | [.number, .title, (.labels | tojson)] | @tsv')

echo "Fetching merged PRs for milestone $milestone..." >&2

while IFS=$'\t' read -r num title author closing_refs
do
    pr_title[$num]="$title"
    pr_author[$num]="$author"

    # Add closing issues from this PR
    while read -r issue_num
    do
        if [[ -n "$issue_num" ]] && [[ -z "${issue_category[$issue_num]:-}" ]]
        then
            issue_json=$(gh issue view "$issue_num" --json title,labels)
            local_title=$(echo "$issue_json" | jq -r '.title')
            local_labels=$(echo "$issue_json" | jq '.labels')
            category=$(categorize_labels "$local_labels")
            add_issue "$issue_num" "$local_title" "$category"
        fi
    done < <(echo "$closing_refs" | jq -r '.[].number' 2>/dev/null)
done < <(gh pr list \
    --state merged \
    --search "milestone:$milestone" \
    --json number,title,author,closingIssuesReferences \
    --jq '.[] | [.number, .title, .author.login, (.closingIssuesReferences | tojson)] | @tsv')

echo "Discovering non-milestoned PRs from git log ($prev_tag..main)..." >&2

while IFS= read -r line
do
    if [[ "$line" =~ Merge\ pull\ request\ \#([0-9]+) ]]
    then
        pr_num="${BASH_REMATCH[1]}"

        if [[ -n "${pr_title[$pr_num]:-}" ]]
        then
            continue
        fi

        pr_json=$(gh pr view "$pr_num" --json title,author,closingIssuesReferences)
        pr_title[$pr_num]=$(echo "$pr_json" | jq -r '.title')
        pr_author[$pr_num]=$(echo "$pr_json" | jq -r '.author.login')

        # Add closing issues from this PR
        while read -r issue_num
        do
            if [[ -n "$issue_num" ]] && [[ -z "${issue_category[$issue_num]:-}" ]]
            then
                issue_json=$(gh issue view "$issue_num" --json title,labels)
                local_title=$(echo "$issue_json" | jq -r '.title')
                local_labels=$(echo "$issue_json" | jq '.labels')
                category=$(categorize_labels "$local_labels")
                add_issue "$issue_num" "$local_title" "$category"
            fi
        done < <(echo "$pr_json" | jq -r '.closingIssuesReferences[].number' 2>/dev/null)
    fi
done < <(git log --oneline "$prev_tag..main")

# Build the output
output=""

append() {
    output+="$1"$'\n'
}

append "## [$new_tag]($repo_url/tree/$new_tag) (UNRELEASED-DATE)"
append ""
append "[Full Changelog]($repo_url/compare/$prev_tag...$new_tag)"

# Enhancements
declare -a enhancement_nums=()
for num in "${!issue_category[@]}"
do
    if [[ "${issue_category[$num]}" == 'enhancement' ]]
    then
        enhancement_nums+=("$num")
    fi
done

if (( ${#enhancement_nums[@]} > 0 ))
then
    IFS=$'\n' read -r -d '' -a enhancement_nums < <(printf '%s\n' "${enhancement_nums[@]}" | sort -n && printf '\0') || true
    append ""
    append '**Implemented enhancements:**'
    append ""
    for num in "${enhancement_nums[@]}"
    do
        append "- ${issue_title[$num]} [\\#$num]($repo_url/issues/$num)"
    done
fi

# Bugs
declare -a bug_nums=()
for num in "${!issue_category[@]}"
do
    if [[ "${issue_category[$num]}" == 'bug' ]]
    then
        bug_nums+=("$num")
    fi
done

if (( ${#bug_nums[@]} > 0 ))
then
    IFS=$'\n' read -r -d '' -a bug_nums < <(printf '%s\n' "${bug_nums[@]}" | sort -n && printf '\0') || true
    append ""
    append '**Fixed bugs:**'
    append ""
    for num in "${bug_nums[@]}"
    do
        append "- ${issue_title[$num]} [\\#$num]($repo_url/issues/$num)"
    done
fi

# Closed issues (other)
declare -a other_nums=()
for num in "${!issue_category[@]}"
do
    if [[ "${issue_category[$num]}" == 'other' ]]
    then
        other_nums+=("$num")
    fi
done

if (( ${#other_nums[@]} > 0 ))
then
    IFS=$'\n' read -r -d '' -a other_nums < <(printf '%s\n' "${other_nums[@]}" | sort -n && printf '\0') || true
    append ""
    append '**Closed issues:**'
    append ""
    for num in "${other_nums[@]}"
    do
        append "- ${issue_title[$num]} [\\#$num]($repo_url/issues/$num)"
    done
fi

# Merged PRs
declare -a pr_nums=()
for num in "${!pr_title[@]}"
do
    pr_nums+=("$num")
done

if (( ${#pr_nums[@]} > 0 ))
then
    IFS=$'\n' read -r -d '' -a pr_nums < <(printf '%s\n' "${pr_nums[@]}" | sort -n && printf '\0') || true
    append ""
    append '**Merged pull requests:**'
    append ""
    for num in "${pr_nums[@]}"
    do
        author="${pr_author[$num]}"
        append "- ${pr_title[$num]} [\\#$num]($repo_url/pull/$num) ([$author](https://github.com/$author))"
    done
fi

echo "Writing to CHANGELOG.md..." >&2

declare -r changelog='CHANGELOG.md'
tmpfile=$(mktemp)
readonly tmpfile

# Insert after the "# Changelog" header line
{
    head -1 "$changelog"
    echo ""
    printf '%s' "$output"
    tail -n +2 "$changelog"
} > "$tmpfile"

mv "$tmpfile" "$changelog"

echo "Done. Review with: git diff CHANGELOG.md" >&2
