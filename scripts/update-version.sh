#!/bin/bash
# Update version across all files
# Usage: ./update-version.sh 2.2.27-beta.4

VERSION="$1"
if [ -z "$VERSION" ]; then
    echo "Usage: $0 <version>"
    echo "Example: $0 2.2.27-beta.4"
    exit 1
fi

echo "Updating version to $VERSION..."

# Extract clean version (x.y.z) for AssemblyVersion
CLEAN_VERSION=$(echo "$VERSION" | sed 's/-.*//')

# Update Directory.Build.props
sed -i "s|<TrackerVersion>.*</TrackerVersion>|<TrackerVersion>$VERSION</TrackerVersion>|g" Directory.Build.props
sed -i "s|<TrackerAssemblyVersion>.*</TrackerAssemblyVersion>|<TrackerAssemblyVersion>$CLEAN_VERSION</TrackerAssemblyVersion>|g" Directory.Build.props
echo "✓ Directory.Build.props"

# Update README.md badge
ESCAPED_VERSION=$(echo "$VERSION" | sed 's/-/--/g')
sed -i -E "s|badge/version-[^-]+|badge/version-$ESCAPED_VERSION|g" README.md
echo "✓ README.md"

# Update scripts/setup.iss
sed -i "s|#define MyAppVersion \".*\"|#define MyAppVersion \"$VERSION\"|g" scripts/setup.iss
echo "✓ scripts/setup.iss"

# Update scripts/publish-app.ps1 comment
sed -i "s|-Version [0-9]\+\.[0-9]\+\.[0-9]\+\([-a-zA-Z0-9.]*\)\?|-Version $VERSION|g" scripts/publish-app.ps1
echo "✓ scripts/publish-app.ps1"

echo ""
echo "Version updated to $VERSION"
echo "Clean version: $CLEAN_VERSION"
