# Mod overlap check - 2026-08-12

Target concept: read-only guild-presence + verified guild bulletin.

Current public searches surfaced **GuildNamePlates** by Recks, an older guild-name display customization mod. It does not provide a guild activity/bulletin system.

ErenshorQoL exposes guild-related commands such as guild invite helpers. Guild Life deliberately does not add or replace guild management commands.

Native Erenshor now has a real Guild Manager, recruitment, guild quests, raid roster/start controls, etc. Guild Life does not replace those systems.

Boundary:

- Native Erenshor owns guild membership and management.
- Guild Life reads verified roster state and presents a bounded local bulletin.
- Other deterministic mods may post facts they already verified.
- Deep Sims may optionally express/react to those facts but should not create authoritative guild history.
