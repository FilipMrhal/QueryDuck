# QueryDuck Rider plugin UI mockups

Design mockups (not live screenshots) showing how the **QueryDuck** JetBrains Rider tool window looks when connected to a running app with `UseQueryDuckDebugging()`.

These images match the layout implemented in `rider-plugin/src/main/kotlin/com/queryduck/rider/QueryDuckPanel.kt` and use the JetBrains Darcula palette from the plugin source.

## Files

| Image | View |
|-------|------|
| [queryduck-rider-main.png](queryduck-rider-main.png) | Default **SQL** tab — captured query table, session warnings, syntax-highlighted SQL, connected status |
| [queryduck-rider-improvements.png](queryduck-rider-improvements.png) | **Improvements** tab — recommendations, suggested index DDL, side-by-side plan step graphs, text diff, pg_stat_statements |

## How to open in Rider

Install the plugin, run your app with the local event server on `http://127.0.0.1:17654`, then open **View → Tool Windows → QueryDuck** (bottom dock).
