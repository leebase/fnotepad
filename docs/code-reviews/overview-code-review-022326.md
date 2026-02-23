pwd && rg --files | head
rg --files
sed -n '1,220p' src/notepad.fs
sed -n '260,520p' src/notepad.fs
sed -n '1,220p' docs/development.md
sed -n '1,220p' CONTRIBUTING.md
sed -n '1,220p' docs/getting-started.md
nl -ba src/notepad.fs | sed -n '1,260p'
nl -ba src/notepad.fs | sed -n '260,520p'
nl -ba rewrite.sh | sed -n '1,120p'
nl -ba format_script.sh | sed -n '1,80p'
nl -ba format_script2.sh | sed -n '1,80p'
nl -ba README.md | sed -n '1,180p'
nl -ba docs/getting-started.md | sed -n '1,200p'
git status --short
nl -ba docs/code-review.md | sed -n '1,220p'
git add docs/code-review.md
git commit -m "docs: add pragmatic code review notes"
