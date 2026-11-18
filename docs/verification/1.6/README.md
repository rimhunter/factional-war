# Verification screenshots, RimWorld 1.6.4871 rev598

Taken windowed with the same scenarios as `docs/verification/1.7` (`scripts/run-scenarios --windowed --save-screenshots DIR -s <name>`), 3840x2160 window, scaled to 1152 pixels wide; `-details` files are native-resolution crops. The 1.7-only click helper is not used: the settings window is captured as opened, not changed.

| File | Scenario | What it shows |
|---|---|---|
| `english-letter-in-the-letter-stack` (+ `-details`) | screenshot-letter-in-the-stack-and-opened-in-english | Letter stack: "Faction Contention" under an earlier "Faction Assault: Farlior, The Bringa League" |
| `english-letter-opened` (+ `-details`) | same | Opened letter text "A batch of precious resources landed nearby, causing two factions to compete." with Close and Jump to location |
| `russian-letter-in-the-letter-stack` (+ `-details`) | screenshot-letter-in-the-stack-and-opened-in-russian | Newest letter reads "Борьба за ресурсы" |
| `russian-letter-opened` (+ `-details`) | same | The letter text in Russian with "Закрыть" and "Перейти к месту" |
| `faction-assault-in-progress` | screenshot-faction-assault-in-progress-with-both-sides-and-its-letter | Faction Assault on the colony map at night: two groups fighting, some downed, letter "Faction Assault: Farlior, The Bringa League" in the stack |
| `battle-site-contention-in-progress` | screenshot-battle-site-contention-in-progress-with-both-sides | Contention site map: a crowd fighting at a cliff edge, a stun marker, downed pawns |
| `battle-site-shelling-in-progress` | screenshot-battle-site-shelling-in-progress-with-both-sides | Bombardment site map: a large burning/explosion area with downed pawns, a group of fighters on the left |
| `battle-site-camp-in-progress` | screenshot-battle-site-camp-in-progress-with-both-sides | Defense (camp) site map: a camp wall at a cliff, defenders and attackers in several small groups (sparse) |
| `battle-site-loot-after-the-battle` | screenshot-loot-lies-on-the-site-map-after-a-battle | Dozens of corpses and dropped weapons and items (forbidden crosses) on the field; messages top left say the area is safe and a faction is fleeing |
| `settings-window-defaults` (+ `-details`) | screenshot-settings-window-shows-the-original-options (1.6 only) | "Factional War" settings: "Enable optimization" ticked, "Threat point factor: 100%", Default and Close |
| `01-mod-list-incompatibility-warning` (+ `-details`) | screenshot-mod-list-warns-when-the-original-factional-war-is-also-enabled | Mod list with the original [SR]Factional War enabled: "Incompatible with: [SR]Factional War"; game versions read "1.6" in green and "1.7" in red |

Not captured on 1.6: Empire and shambler battles (Royalty and Anomaly scenarios are 1.7-only), the changed settings state (needs the 1.7 click).

## Known limits

- Vanilla log-error flakes. The runner (not these scenarios) fails a scenario on any `Log.Error` raised while it runs, and RimWorld itself logs errors in battles with only vanilla frames in the stack: `Tried to destroy already-destroyed thing Fire<id>`, `Spawning destroyed thing Gun_TripleRocket...` (with its two companion lines), `Job N faulted with exception: NullReferenceException` in `PawnUtility.PawnBlockedBy`, `Never haulable ActiveDropPod... blocking Blueprint_Turret_Mortar...`, and, with Anomaly, an `IndexOutOfRangeException` from `CompAbilityEffect_Burner`. Measured rate: about 1 failing execution in 10 of the battle scenarios on 1.7; on 1.6 the loot scenario failed 3 of 5 attempts on the Fire and TripleRocket errors.
- No full pass was clean in one run on either version; each full pass had one to two failures, and every scenario passed at least once.
- Other measured flakes (1.7): `player-side-harming-a-friendly-side...` failed in setup once in about 8 runs; `player-side-helping-a-side-with-an-allied-faction...` passed 1 of 3 in one sequence; battles on the 250x250 caravan site maps sometimes stall or end without a fight (about 6 of 16 early loot attempts, 3 of about 25 in-progress attempts).
- A follow-up in the shared runner (`rimhunter-testrunner`) is needed for an expected-error list.
