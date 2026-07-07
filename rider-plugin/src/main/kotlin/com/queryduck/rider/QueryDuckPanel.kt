package com.queryduck.rider

import com.intellij.openapi.ide.CopyPasteManager
import com.intellij.openapi.project.Project
import com.intellij.ui.JBSplitter
import com.intellij.ui.components.JBLabel
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.components.JBTabbedPane
import com.intellij.ui.table.JBTable
import com.intellij.util.ui.JBUI
import java.awt.BorderLayout
import java.awt.Color
import java.awt.Component
import java.awt.Dimension
import java.awt.FlowLayout
import java.awt.datatransfer.StringSelection
import java.awt.event.KeyAdapter
import java.awt.event.KeyEvent
import javax.swing.BorderFactory
import javax.swing.Box
import javax.swing.DefaultListCellRenderer
import javax.swing.DefaultListModel
import javax.swing.JButton
import javax.swing.JCheckBox
import javax.swing.JComboBox
import javax.swing.JLabel
import javax.swing.JList
import javax.swing.JPanel
import javax.swing.JTable
import javax.swing.JTextField
import javax.swing.ListSelectionModel
import javax.swing.SwingUtilities
import javax.swing.Timer
import javax.swing.table.AbstractTableModel

class QueryDuckPanel(private val project: Project) : JPanel(BorderLayout()) {
    private var client = QueryDuckEventClient(DEFAULT_SERVER_URL)
    private val events = mutableListOf<QueryCaptureEventDto>()
    private val tableModel = EventTableModel()
    private val table = JBTable(tableModel).apply {
        selectionModel.selectionMode = ListSelectionModel.SINGLE_SELECTION
        rowHeight = 26
        setShowGrid(false)
        intercellSpacing = Dimension(0, 0)
        setDefaultRenderer(Any::class.java, EventTableCellRenderer())
        columnModel.getColumn(0).preferredWidth = 72
        columnModel.getColumn(1).preferredWidth = 84
        columnModel.getColumn(2).preferredWidth = 110
        columnModel.getColumn(3).preferredWidth = 52
        columnModel.getColumn(4).preferredWidth = 52
        columnModel.getColumn(5).preferredWidth = 420
        addMouseListener(object : java.awt.event.MouseAdapter() {
            override fun mouseClicked(e: java.awt.event.MouseEvent) {
                if (e.clickCount == 2) {
                    tabs.selectedIndex = 0
                }
            }
        })
    }

    private val sessionWarningsLabel = JBLabel(" ").apply {
        foreground = Color(0xE8A035)
        border = JBUI.Borders.empty(2, 8)
    }

    private val statusLabel = JBLabel("Disconnected").apply {
        foreground = Color(0xE05555)
    }

    private val serverUrlField = JTextField(DEFAULT_SERVER_URL, 24).apply {
        toolTipText = "QueryDuck event server base URL"
    }

    private val autoRefresh = JCheckBox("Auto-refresh", true)
    private val followLive = JCheckBox("Follow latest", true)
    private val providerFilter = JComboBox(arrayOf("All providers", "Oracle", "PostgreSql", "SqlServer", "MySql"))
    private val tagFilter = JTextField(12).apply {
        toolTipText = "Filter by TagWith tag"
        addKeyListener(object : KeyAdapter() {
            override fun keyReleased(e: KeyEvent) {
                applyFilters(preserveSelection = true)
            }
        })
    }

    private val sqlPanel = QueryDuckCodeEditor.sqlPanel(project)
    private val csharpPanel = QueryDuckCodeEditor.csharpPanel(project)
    private val planPanel = QueryDuckCodeEditor.planPanel(project)
    private val improvementsPanel = QueryDuckCodeEditor.planPanel(project)
    private val planGraphPanel = JPanel(BorderLayout())
    private val pgStatPanel = QueryDuckCodeEditor.planPanel(project)
    private val suggestedSqlPanel = QueryDuckCodeEditor.sqlPanel(project)
    private val metaLabel = JBLabel(" ").apply { border = JBUI.Borders.empty(6, 8) }

    private val recommendationsList = JList<SlowQueryRecommendationDto>().apply {
        cellRenderer = RecommendationRenderer()
    }

    private val warningsList = JList<QueryDiagnosticDto>().apply {
        cellRenderer = DiagnosticRenderer()
    }

    private val parametersTable = JBTable(ParametersTableModel()).apply {
        rowHeight = 22
    }

    private val expressionTreePanel = JPanel(BorderLayout())
    private val tabs = JBTabbedPane().apply {
        addTab("SQL", sqlPanel)
        addTab("Expression Tree", expressionTreePanel)
        addTab("C# Expression", csharpPanel)
        addTab("Diagnostics", JBScrollPane(warningsList))
        addTab("Parameters", JBScrollPane(parametersTable))
        addTab("Plan", planPanel)
        addTab("Improvements", buildImprovementsPanel())
    }

    private fun buildImprovementsPanel(): JPanel =
        JPanel(BorderLayout()).apply {
            val listPane = JBScrollPane(recommendationsList).apply {
                border = BorderFactory.createTitledBorder("Recommendations")
                preferredSize = Dimension(220, 120)
            }
            val detailPane = JPanel(BorderLayout()).apply {
                add(JBScrollPane(suggestedSqlPanel).apply {
                    border = BorderFactory.createTitledBorder("Suggested SQL / index DDL")
                }, BorderLayout.NORTH)
                add(
                    JPanel(BorderLayout()).apply {
                        add(planGraphPanel, BorderLayout.CENTER)
                        add(JBScrollPane(improvementsPanel).apply {
                            border = BorderFactory.createTitledBorder("Plan comparison (text)")
                            preferredSize = Dimension(400, 160)
                        }, BorderLayout.SOUTH)
                    },
                    BorderLayout.CENTER,
                )
                add(JBScrollPane(pgStatPanel).apply {
                    border = BorderFactory.createTitledBorder("pg_stat_statements (opt-in)")
                    preferredSize = Dimension(400, 100)
                }, BorderLayout.SOUTH)
            }
            add(listPane, BorderLayout.WEST)
            add(detailPane, BorderLayout.CENTER)
            recommendationsList.addListSelectionListener {
                if (!it.valueIsAdjusting) {
                    showSelectedRecommendation()
                }
            }
        }

    private val refreshTimer = Timer(2000) { refresh(silent = true) }
    private var selectedEventId: String? = null
    private var newestEventId: String? = null

    init {
        border = JBUI.Borders.empty(4)
        add(buildToolbar(), BorderLayout.NORTH)

        val splitter = JBSplitter(false, 0.42f).apply {
            firstComponent = JBScrollPane(table).apply {
                border = BorderFactory.createTitledBorder("Captured queries")
            }
            secondComponent = JPanel(BorderLayout()).apply {
                add(metaLabel, BorderLayout.NORTH)
                add(tabs, BorderLayout.CENTER)
            }
        }

        val body = JPanel(BorderLayout()).apply {
            add(sessionWarningsLabel, BorderLayout.NORTH)
            add(splitter, BorderLayout.CENTER)
        }

        add(body, BorderLayout.CENTER)
        add(buildFooter(), BorderLayout.SOUTH)

        table.selectionModel.addListSelectionListener {
            if (!it.valueIsAdjusting) {
                selectedEventId = tableModel.getEventAt(table.selectedRow)?.eventId
                showSelectedEvent()
            }
        }

        providerFilter.addActionListener { applyFilters(preserveSelection = true) }
        autoRefresh.addActionListener {
            if (autoRefresh.isSelected) refreshTimer.start() else refreshTimer.stop()
        }

        refreshTimer.start()
        refresh(silent = false)
    }

    private fun buildToolbar(): JPanel {
        val refreshButton = JButton("Refresh").apply { addActionListener { refresh(silent = false) } }
        val clearButton = JButton("Clear").apply { addActionListener { clearEvents() } }
        val connectButton = JButton("Connect").apply {
            addActionListener {
                client = QueryDuckEventClient(serverUrlField.text.trim().ifBlank { DEFAULT_SERVER_URL })
                refresh(silent = false)
            }
        }

        return JPanel(FlowLayout(FlowLayout.LEFT, 8, 4)).apply {
            add(refreshButton)
            add(clearButton)
            add(autoRefresh)
            add(followLive)
            add(JLabel("Server"))
            add(serverUrlField)
            add(connectButton)
            add(JLabel("Provider"))
            add(providerFilter)
            add(JLabel("Tag"))
            add(tagFilter)
            add(Box.createHorizontalStrut(8))
            add(statusLabel)
        }
    }

    private fun buildFooter(): JPanel =
        JPanel(BorderLayout()).apply {
            border = JBUI.Borders.emptyTop(4)
            add(
                JBLabel(
                    "Tip: options.UseQueryDuckDebugging() auto-captures all queries — no WithQueryDuckScope() needed.",
                ),
                BorderLayout.WEST,
            )
        }

    private fun refresh(silent: Boolean) {
        Thread {
            try {
                val health = client.fetchHealth()
                val fetched = client.fetchEvents()
                SwingUtilities.invokeLater {
                    statusLabel.text = "Connected · ${health.count} event(s) · ${client.baseUrl}"
                    statusLabel.foreground = Color(0x4EBF82)
                    sessionWarningsLabel.text = if (health.sessionWarnings.isEmpty()) {
                        " "
                    } else {
                        " Session: ${health.sessionWarnings.joinToString(" | ")}"
                    }
                    val previousNewest = newestEventId
                    events.clear()
                    events.addAll(fetched.asReversed())
                    newestEventId = events.firstOrNull()?.eventId
                    val hasNewEvents = newestEventId != null && newestEventId != previousNewest
                    applyFilters(
                        preserveSelection = true,
                        preferNewest = hasNewEvents && followLive.isSelected,
                    )
                    if (!silent && table.rowCount > 0 && table.selectedRow < 0) {
                        selectEventById(newestEventId)
                    }
                }
            } catch (ex: Exception) {
                SwingUtilities.invokeLater {
                    statusLabel.text = "Disconnected · ${ex.message ?: "QueryDuck server not running"}"
                    statusLabel.foreground = Color(0xE05555)
                    sessionWarningsLabel.text = " "
                    if (!silent) {
                        metaLabel.text = " Start your app with UseQueryDuckDebugging() or UseQueryDuckCapture(o => o.StartLocalEventServer = true)"
                    }
                }
            }
        }.start()
    }

    private fun clearEvents() {
        Thread {
            try {
                client.clearEvents()
                SwingUtilities.invokeLater {
                    events.clear()
                    newestEventId = null
                    selectedEventId = null
                    tableModel.setRows(emptyList())
                    clearDetails()
                }
            } catch (ex: Exception) {
                SwingUtilities.invokeLater {
                    statusLabel.text = "Clear failed: ${ex.message}"
                    statusLabel.foreground = Color(0xE05555)
                }
            }
        }.start()
    }

    private fun applyFilters(preserveSelection: Boolean, preferNewest: Boolean = false) {
        val provider = providerFilter.selectedItem?.toString().orEmpty()
        val tag = tagFilter.text.trim().lowercase()

        val filtered = events.filter { event ->
            val providerMatch = provider == "All providers" || event.provider.equals(provider, ignoreCase = true)
            val tagMatch = tag.isBlank() || event.tag.orEmpty().lowercase().contains(tag)
            providerMatch && tagMatch
        }

        tableModel.setRows(filtered)

        val targetId = when {
            preferNewest -> filtered.firstOrNull()?.eventId
            preserveSelection && selectedEventId != null && filtered.any { it.eventId == selectedEventId } -> selectedEventId
            filtered.isNotEmpty() -> filtered.first().eventId
            else -> null
        }

        selectEventById(targetId)
    }

    private fun selectEventById(eventId: String?) {
        if (eventId == null) {
            clearDetails()
            return
        }

        val row = tableModel.indexOf(eventId)
        if (row >= 0) {
            table.selectionModel.setSelectionInterval(row, row)
        } else {
            showSelectedEvent()
        }
    }

    private fun showSelectedEvent() {
        val row = table.selectedRow
        if (row < 0) {
            clearDetails()
            return
        }

        val event = tableModel.getEventAt(row) ?: return
        selectedEventId = event.eventId
        metaLabel.text = buildMetaLabel(event)

        sqlPanel.setText(formatSql(event.sql))
        csharpPanel.setText(
            event.expressionCSharp ?: "-- No C# expression captured.\n-- Enable UseQueryDuckDebugging() or call .WithQueryDuckScope(context) before executing.",
        )
        planPanel.setText(
            event.executionPlan ?: "-- No execution plan captured.\n-- Slow queries auto-capture plans when CapturePlansForSlowQueries is enabled.",
        )
        showImprovementAnalysis(event)

        warningsList.model = DefaultListModel<QueryDiagnosticDto>().apply {
            if (event.diagnostics.isEmpty()) {
                addElement(QueryDiagnosticDto("INFO", "Info", "No diagnostics for this query."))
            } else {
                event.diagnostics.forEach { addElement(it) }
            }
        }

        (parametersTable.model as ParametersTableModel).setParameters(event.parameters)
        expressionTreePanel.removeAll()
        expressionTreePanel.add(buildExpressionTreeSection(event), BorderLayout.CENTER)
        expressionTreePanel.revalidate()
        expressionTreePanel.repaint()
    }

    private fun showImprovementAnalysis(event: QueryCaptureEventDto) {
        val analysis = event.improvementAnalysis
        if (analysis == null) {
            recommendationsList.model = DefaultListModel()
            suggestedSqlPanel.setText("-- No slow-query analysis for this event.\n-- Analysis runs when duration exceeds SlowQueryThresholdMs.")
            improvementsPanel.setText("-- Plan comparison appears here when a rewrite is recommended.")
            pgStatPanel.setText("-- Enable EnablePgStatStatementsInsights for PostgreSQL historical stats.")
            planGraphPanel.removeAll()
            planGraphPanel.revalidate()
            return
        }

        recommendationsList.model = DefaultListModel<SlowQueryRecommendationDto>().apply {
            analysis.recommendations.forEach { addElement(it) }
        }

        pgStatPanel.setText(formatPgStatInsight(analysis.pgStatStatements))

        if (analysis.recommendations.isNotEmpty()) {
            recommendationsList.selectedIndex = 0
        } else {
            suggestedSqlPanel.setText("-- No recommendations generated.")
            improvementsPanel.setText(analysis.primaryPlanDiff?.textDiff ?: "-- No plan diff available.")
            renderPlanGraph(analysis.primaryPlanDiff)
        }
    }

    private fun formatPgStatInsight(insight: PgStatStatementInsightDto?): String {
        if (insight == null) {
            return "-- pg_stat_statements not included.\n-- Set EnablePgStatStatementsInsights = true and register AddPostgreSql() adapters."
        }

        return buildString {
            appendLine("-- Matched pg_stat_statements entry")
            appendLine("calls: ${insight.calls}")
            appendLine("mean_exec_time_ms: ${"%.1f".format(insight.meanExecTimeMs)}")
            appendLine("total_exec_time_ms: ${"%.0f".format(insight.totalExecTimeMs)}")
            appendLine("rows: ${insight.rows}")
            appendLine("shared_blocks_hit_ratio: ${"%.0f".format(insight.sharedBlocksHitRatio * 100)}%")
            if (!insight.matchedQueryText.isNullOrBlank()) {
                appendLine()
                appendLine("-- matched query")
                append(insight.matchedQueryText)
            }
        }
    }

    private fun renderPlanGraph(planDiff: PlanDiffVisualizationDto?) {
        planGraphPanel.removeAll()
        planGraphPanel.add(
            PlanFlowGraphPanelFactory.createSideBySidePanel(project, planDiff),
            BorderLayout.CENTER,
        )
        planGraphPanel.revalidate()
        planGraphPanel.repaint()
    }

    private fun showSelectedRecommendation() {
        val recommendation = recommendationsList.selectedValue ?: return
        val sqlText = buildString {
            if (!recommendation.suggestedSql.isNullOrBlank()) {
                appendLine("-- Suggested rewrite")
                appendLine(recommendation.suggestedSql)
                appendLine()
            }
            if (!recommendation.suggestedIndexSql.isNullOrBlank()) {
                appendLine("-- Suggested index / schema change")
                appendLine(recommendation.suggestedIndexSql)
            }
        }
        suggestedSqlPanel.setText(sqlText.ifBlank { "-- ${recommendation.title}\n-- ${recommendation.description}" })
        improvementsPanel.setText(
            recommendation.planDiff?.textDiff
                ?: eventPlanDiffFallback(recommendation),
        )
        renderPlanGraph(recommendation.planDiff ?: findSelectedEvent()?.improvementAnalysis?.primaryPlanDiff)
    }

    private fun findSelectedEvent(): QueryCaptureEventDto? =
        selectedEventId?.let { id -> events.firstOrNull { it.eventId == id } }

    private fun eventPlanDiffFallback(recommendation: SlowQueryRecommendationDto): String =
        recommendation.improvedPlanText?.let { improved ->
            buildString {
                appendLine("=== Improved EXPLAIN output ===")
                appendLine(improved)
            }
        } ?: "-- Run the suggested SQL with EXPLAIN on your database to compare plans."

    private fun buildExpressionTreeSection(event: QueryCaptureEventDto): JPanel =
        JPanel(BorderLayout()).apply {
            add(
                JPanel(BorderLayout()).apply {
                    border = JBUI.Borders.empty(0, 0, 4, 0)
                    add(
                        JButton("Copy tree").apply {
                            addActionListener {
                                val text = event.expressionTreeText ?: event.expressionCSharp.orEmpty()
                                CopyPasteManager.getInstance().setContents(StringSelection(text))
                            }
                        },
                        BorderLayout.EAST,
                    )
                },
                BorderLayout.NORTH,
            )
            add(ExpressionTreePanelFactory.createPanel(event), BorderLayout.CENTER)
        }

    private fun buildMetaLabel(event: QueryCaptureEventDto): String {
        val source = when {
            event.source.equals("EntityFrameworkExtensions", ignoreCase = true) -> {
                val op = event.bulkOperation ?: event.caller ?: "Bulk"
                "EF Extensions · $op"
            }
            else -> event.caller ?: "Unknown source"
        }
        return " ${event.provider} · $source · ${event.formattedDuration()} · ${event.eventId.take(8)} · schema v${event.schemaVersion}"
    }

    private fun clearDetails() {
        metaLabel.text = " "
        sqlPanel.setText("")
        csharpPanel.setText("")
        planPanel.setText("")
        suggestedSqlPanel.setText("")
        improvementsPanel.setText("")
        pgStatPanel.setText("")
        planGraphPanel.removeAll()
        planGraphPanel.revalidate()
        recommendationsList.model = DefaultListModel()
        warningsList.model = DefaultListModel()
        (parametersTable.model as ParametersTableModel).setParameters(emptyMap())
        expressionTreePanel.removeAll()
        expressionTreePanel.revalidate()
    }

    private fun formatSql(sql: String): String =
        sql.replace(" SELECT ", "\nSELECT ")
            .replace(" FROM ", "\nFROM ")
            .replace(" WHERE ", "\nWHERE ")
            .replace(" INNER JOIN ", "\nINNER JOIN ")
            .replace(" LEFT JOIN ", "\nLEFT JOIN ")
            .replace(" ORDER BY ", "\nORDER BY ")
            .replace(" GROUP BY ", "\nGROUP BY ")
            .trim()

    private inner class EventTableModel : AbstractTableModel() {
        private val columns = arrayOf("Time", "Provider", "Tag", "Warn", "Ms", "SQL")
        private var rows: List<QueryCaptureEventDto> = emptyList()

        fun setRows(data: List<QueryCaptureEventDto>) {
            rows = data
            fireTableDataChanged()
        }

        fun getEventAt(viewRow: Int): QueryCaptureEventDto? = rows.getOrNull(viewRow)

        fun indexOf(eventId: String): Int = rows.indexOfFirst { it.eventId == eventId }

        override fun getRowCount(): Int = rows.size

        override fun getColumnCount(): Int = columns.size

        override fun getColumnName(column: Int): String = columns[column]

        override fun getValueAt(rowIndex: Int, columnIndex: Int): Any {
            val event = rows[rowIndex]
            return when (columnIndex) {
                0 -> event.formattedTime()
                1 -> event.provider
                2 -> event.tag ?: "—"
                3 -> if (event.warningCount > 0) event.warningCount.toString() else "·"
                4 -> event.formattedDuration()
                else -> event.sqlPreview()
            }
        }
    }

    private class ParametersTableModel : AbstractTableModel() {
        private val columns = arrayOf("Parameter", "Value")
        private var entries: List<Pair<String, String>> = emptyList()

        fun setParameters(parameters: Map<String, Any?>) {
            entries = parameters.entries.map { it.key to (it.value?.toString() ?: "NULL") }
            fireTableDataChanged()
        }

        override fun getRowCount(): Int = entries.size

        override fun getColumnCount(): Int = columns.size

        override fun getColumnName(column: Int): String = columns[column]

        override fun getValueAt(rowIndex: Int, columnIndex: Int): Any =
            if (columnIndex == 0) entries[rowIndex].first else entries[rowIndex].second
    }

    private class RecommendationRenderer : DefaultListCellRenderer() {
        override fun getListCellRendererComponent(
            list: JList<*>?,
            value: Any?,
            index: Int,
            isSelected: Boolean,
            cellHasFocus: Boolean,
        ): Component {
            val component = super.getListCellRendererComponent(list, value, index, isSelected, cellHasFocus)
            if (value is SlowQueryRecommendationDto) {
                text = "[${value.category}] ${value.title}"
            }
            return component
        }
    }

    private class DiagnosticRenderer : DefaultListCellRenderer() {
        override fun getListCellRendererComponent(
            list: JList<*>?,
            value: Any?,
            index: Int,
            isSelected: Boolean,
            cellHasFocus: Boolean,
        ): Component {
            val component = super.getListCellRendererComponent(list, value, index, isSelected, cellHasFocus)
            if (value is QueryDiagnosticDto) {
                text = "[${value.ruleId}] ${value.message}"
                if (!value.fixHint.isNullOrBlank()) {
                    text = "$text — ${value.fixHint}"
                }
                if (!isSelected) {
                    foreground = when (value.severity.lowercase()) {
                        "error" -> Color(0xE05555)
                        "warning" -> Color(0xE8A035)
                        else -> Color(0x8FA2C3)
                    }
                }
            }
            return component
        }
    }

    companion object {
        private const val DEFAULT_SERVER_URL = "http://127.0.0.1:17654"
    }
}
