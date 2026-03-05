#!/bin/bash
set -euo pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
SERVER=sqlserver
USER=sa

# ---------------------------------------------------------------------------
# Helper: run a sqlcmd query and return trimmed output
# ---------------------------------------------------------------------------
run_query() {
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -h -1 -No -Q "$1" | tr -d '[:space:]'
}

# ---------------------------------------------------------------------------
# CarrotCoreMVC database
# ---------------------------------------------------------------------------
echo "[init] Checking for CarrotCoreMVC..."

DB_EXISTS=$(run_query "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name='CarrotCoreMVC'")

if [ "$DB_EXISTS" = "0" ]; then
    echo "[init] Creating CarrotCoreMVC database..."
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -No \
        -Q "CREATE DATABASE [CarrotCoreMVC]"

    echo "[init] Running table scripts..."

    # Tier 0 — Independent (no FK dependencies)
    echo "[init]   Tier 0: __EFMigrationsHistory"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/__EFMigrationsHistory.sql"
    echo "[init]   Tier 0: AspNetCache"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/AspNetCache.sql"
    echo "[init]   Tier 0: AspNetRoles"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/AspNetRoles.sql"
    echo "[init]   Tier 0: AspNetUsers"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/AspNetUsers.sql"
    echo "[init]   Tier 0: carrot_ContentType"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_ContentType.sql"
    echo "[init]   Tier 0: carrot_SerialCache"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_SerialCache.sql"
    echo "[init]   Tier 0: carrot_Sites"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_Sites.sql"

    # Tier 1 — Depend on Tier 0
    echo "[init]   Tier 1: AspNetRoleClaims"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/AspNetRoleClaims.sql"
    echo "[init]   Tier 1: AspNetUserClaims"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/AspNetUserClaims.sql"
    echo "[init]   Tier 1: AspNetUserLogins"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/AspNetUserLogins.sql"
    echo "[init]   Tier 1: AspNetUserRoles"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/AspNetUserRoles.sql"
    echo "[init]   Tier 1: AspNetUserTokens"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/AspNetUserTokens.sql"
    echo "[init]   Tier 1: carrot_UserData"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_UserData.sql"
    echo "[init]   Tier 1: carrot_ContentCategory"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_ContentCategory.sql"
    echo "[init]   Tier 1: carrot_ContentTag"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_ContentTag.sql"
    echo "[init]   Tier 1: carrot_RootContentSnippet"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_RootContentSnippet.sql"
    echo "[init]   Tier 1: carrot_TextWidget"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_TextWidget.sql"

    # Tier 2 — Depend on Tier 1
    echo "[init]   Tier 2: carrot_RootContent"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_RootContent.sql"
    echo "[init]   Tier 2: carrot_UserSiteMapping"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_UserSiteMapping.sql"
    echo "[init]   Tier 2: carrot_ContentSnippet"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_ContentSnippet.sql"

    # Tier 3 — Depend on Tier 2
    echo "[init]   Tier 3: carrot_Content"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_Content.sql"
    echo "[init]   Tier 3: carrot_ContentComment"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_ContentComment.sql"
    echo "[init]   Tier 3: carrot_Widget"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_Widget.sql"
    echo "[init]   Tier 3: carrot_CategoryContentMapping"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_CategoryContentMapping.sql"
    echo "[init]   Tier 3: carrot_TagContentMapping"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_TagContentMapping.sql"

    # Tier 4 — Depend on Tier 3
    echo "[init]   Tier 4: carrot_WidgetData"
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
        -i "/scripts/carrot/dbo/Tables/carrot_WidgetData.sql"

    echo "[init] Running view scripts..."
    # Tier 0: base views (no view dependencies)
    for v in vw_carrot_Content vw_carrot_UserData vw_carrot_CategoryCounted \
              vw_carrot_ContentSnippet vw_carrot_EditHistory \
              vw_carrot_TagCounted vw_carrot_Widget; do
        echo "[init]   View: ${v}.sql"
        "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
            -i "/scripts/carrot/dbo/Views/${v}.sql"
    done
    # Tier 1: views that depend on vw_carrot_Content / vw_carrot_UserData
    for v in vw_carrot_CategoryURL vw_carrot_Comment vw_carrot_ContentChild \
              vw_carrot_TagURL vw_carrot_EditorURL; do
        echo "[init]   View: ${v}.sql"
        "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
            -i "/scripts/carrot/dbo/Views/${v}.sql"
    done

    echo "[init] Running stored procedure scripts..."
    while IFS= read -r f; do
        echo "[init]   Proc: $(basename "$f")"
        "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -I -No \
            -i "$f"
    done < <(find "/scripts/carrot/dbo/Stored Procedures/" -name "*.sql" | sort)

    echo "[init] Seeding EF Core migration history..."
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -d CarrotCoreMVC -b -No -Q \
        "INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId],[ProductVersion]) VALUES
         ('00000000000000_Initial','7.0.7'),
         ('20230610172319_AddNewAuth','7.0.7'),
         ('20230611023834_AuthUserFK','7.0.7'),
         ('20230618191117_FixAuthTables','7.0.7'),
         ('20230621040026_Cleanup','7.0.7'),
         ('20230627020957_UpdateRoleUserJoin','7.0.7'),
         ('20230708202544_CreateSession','6.0.18');"

    echo "[init] CarrotCoreMVC initialized."
else
    echo "[init] CarrotCoreMVC already exists, skipping."
fi

# ---------------------------------------------------------------------------
# Northwind database
# ---------------------------------------------------------------------------
echo "[init] Checking for Northwind..."

NW_EXISTS=$(run_query "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name='Northwind'")

if [ "$NW_EXISTS" = "0" ]; then
    echo "[init] Creating Northwind database..."
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -No \
        -Q "CREATE DATABASE [Northwind]"

    echo "[init] Running Northwind script..."
    "$SQLCMD" -S "$SERVER" -U "$USER" -P "$SA_PASSWORD" -b -I -No \
        -i "/scripts/northwind/northwind.sql"

    echo "[init] Northwind initialized."
else
    echo "[init] Northwind already exists, skipping."
fi

echo "[init] Done."
exit 0
