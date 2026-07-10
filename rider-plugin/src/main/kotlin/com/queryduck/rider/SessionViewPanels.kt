package com.queryduck.rider

import com.intellij.openapi.project.Project
import com.intellij.ui.table.JBTable
import java.awt.BorderLayout
import javax.swing.JPanel
import javax.swing.JScrollPane
import javax.swing.table.AbstractTableModel

object SessionViewPanels {
    fun createHotspotsPanel(project: Project): HotspotsPanel = HotspotsPanel(project)

    fun createTimelinePanel(project: Project): TimelinePanel = TimelinePanel(project)

    fun createTracesPanel(project: Project): TracesPanel = TracesPanel(project)

    fun createDiffPanel(project: Project): DiffPanel = DiffPanel(project)

    fun createStatementCachePanel(project: Project): StatementCachePanel = StatementCachePanel(project)
}

class HotspotsPanel(project: Project) : JPanel(BorderLayout()) {
    private val summaryLabel = com.intellij.ui.components.JBLabel(" ")
    private val tableModel = HotspotsTableModel()
    private val table = JBTable(tableModel).apply {
        setShowGrid(false)
        rowHeight = 24
    }

    init {
        add(summaryLabel, BorderLayout.NORTH)
        add(JScrollPane(table), BorderLayout.CENTER)
    }

    fun showData(data: QueryDuckSessionHotspotsDto) {
        summaryLabel.text =
            " ${data.totalEvents} event(s) · ${data.distinctShapes} distinct shape(s) · top ${data.hotspots.size}"
        tableModel.setRows(data.hotspots)
    }

    fun showMessage(message: String) {
        summaryLabel.text = " $message"
        tableModel.setRows(emptyList())
    }
}

class TimelinePanel(project: Project) : JPanel(BorderLayout()) {
    private val summaryLabel = com.intellij.ui.components.JBLabel(" ")
    private val tableModel = TimelineTableModel()
    private val table = JBTable(tableModel).apply {
        setShowGrid(false)
        rowHeight = 24
    }

    init {
        add(summaryLabel, BorderLayout.NORTH)
        add(JScrollPane(table), BorderLayout.CENTER)
    }

    fun showEntries(entries: List<QueryDuckTimelineEntryDto>) {
        summaryLabel.text = " ${entries.size} timeline entry(ies)"
        tableModel.setRows(entries)
    }

    fun showMessage(message: String) {
        summaryLabel.text = " $message"
        tableModel.setRows(emptyList())
    }
}

class TracesPanel(project: Project) : JPanel(BorderLayout()) {
    private val summaryLabel = com.intellij.ui.components.JBLabel(" ")
    private val tableModel = TracesTableModel()
    private val table = JBTable(tableModel).apply {
        setShowGrid(false)
        rowHeight = 24
    }

    init {
        add(summaryLabel, BorderLayout.NORTH)
        add(JScrollPane(table), BorderLayout.CENTER)
    }

    fun showData(data: QueryDuckTraceGroupingDto) {
        summaryLabel.text = " ${data.totalEvents} event(s) · ${data.groupCount} trace group(s)"
        tableModel.setRows(data.groups)
    }

    fun showMessage(message: String) {
        summaryLabel.text = " $message"
        tableModel.setRows(emptyList())
    }
}

class DiffPanel(project: Project) : JPanel(BorderLayout()) {
    private val summaryPanel = com.intellij.ui.components.JBLabel(" ")
    private val leftSqlPanel = QueryDuckCodeEditor.sqlPanel(project)
    private val rightSqlPanel = QueryDuckCodeEditor.sqlPanel(project)
    private val parametersPanel = QueryDuckCodeEditor.planPanel(project)
    private val diagnosticsPanel = QueryDuckCodeEditor.planPanel(project)

    init {
        val sqlSplit = com.intellij.ui.JBSplitter(false, 0.5f).apply {
            firstComponent = leftSqlPanel.apply { border = javax.swing.BorderFactory.createTitledBorder("Left SQL") }
            secondComponent = rightSqlPanel.apply { border = javax.swing.BorderFactory.createTitledBorder("Right SQL") }
        }
        val detailSplit = com.intellij.ui.JBSplitter(false, 0.5f).apply {
            firstComponent = parametersPanel.apply { border = javax.swing.BorderFactory.createTitledBorder("Parameter diff") }
            secondComponent = diagnosticsPanel.apply { border = javax.swing.BorderFactory.createTitledBorder("Diagnostic diff") }
        }
        add(summaryPanel, BorderLayout.NORTH)
        add(sqlSplit, BorderLayout.CENTER)
        add(detailSplit, BorderLayout.SOUTH)
        detailSplit.preferredSize = java.awt.Dimension(400, 180)
    }

    fun showDiff(diff: QueryCaptureEventDiffDto) {
        summaryPanel.text = buildString {
            append(" left=${diff.left.eventId.take(8)} · right=${diff.right.eventId.take(8)}")
            val changes = buildList {
                if (diff.sqlChanged) add("SQL")
                if (diff.parametersChanged) add("parameters")
                if (diff.planChanged) add("plan")
                if (diff.diagnosticsChanged) add("diagnostics")
                if (diff.durationChanged) add("duration")
            }
            if (changes.isNotEmpty()) {
                append(" · changed: ${changes.joinToString(", ")}")
            } else {
                append(" · no changes")
            }
        }
        leftSqlPanel.setText(diff.left.sql)
        rightSqlPanel.setText(diff.right.sql)
        parametersPanel.setText(diff.parametersDiff)
        diagnosticsPanel.setText(diff.diagnosticsDiff)
    }

    fun showMessage(message: String) {
        summaryPanel.text = " $message"
        leftSqlPanel.setText("")
        rightSqlPanel.setText("")
        parametersPanel.setText("")
        diagnosticsPanel.setText("")
    }
}

class StatementCachePanel(project: Project) : JPanel(BorderLayout()) {
    private val summaryLabel = com.intellij.ui.components.JBLabel(" ")
    private val tableModel = StatementCacheTableModel()
    private val table = JBTable(tableModel).apply {
        setShowGrid(false)
        rowHeight = 24
    }

    init {
        add(summaryLabel, BorderLayout.NORTH)
        add(JScrollPane(table), BorderLayout.CENTER)
    }

    fun showData(data: QueryDuckStatementCacheDiagnosticsDto) {
        summaryLabel.text = if (data.connectionAvailable) {
            " Provider: ${data.provider} · ${data.findings.size} finding(s)"
        } else {
            " No active database connection yet — run a query with provider adapters registered."
        }
        tableModel.setRows(data.findings)
    }

    fun showMessage(message: String) {
        summaryLabel.text = " $message"
        tableModel.setRows(emptyList())
    }
}

private class HotspotsTableModel : AbstractTableModel() {
    private val columns = arrayOf("Shape", "Count", "Total ms", "Avg ms", "Max ms", "Providers", "Preview")
    private var rows: List<QueryShapeHotspotDto> = emptyList()

    fun setRows(data: List<QueryShapeHotspotDto>) {
        rows = data
        fireTableDataChanged()
    }

    override fun getRowCount(): Int = rows.size

    override fun getColumnCount(): Int = columns.size

    override fun getColumnName(column: Int): String = columns[column]

    override fun getValueAt(rowIndex: Int, columnIndex: Int): Any =
        when (columnIndex) {
            0 -> rows[rowIndex].shapeKey
            1 -> rows[rowIndex].executionCount
            2 -> "%.0f".format(rows[rowIndex].totalDurationMs)
            3 -> "%.1f".format(rows[rowIndex].averageDurationMs)
            4 -> "%.0f".format(rows[rowIndex].maxDurationMs)
            5 -> rows[rowIndex].providers.joinToString(", ")
            else -> rows[rowIndex].normalizedSqlPreview
        }
}

private class TimelineTableModel : AbstractTableModel() {
    private val columns = arrayOf("Time", "Kind", "Label", "Ms", "Trace", "Event")
    private var rows: List<QueryDuckTimelineEntryDto> = emptyList()

    fun setRows(data: List<QueryDuckTimelineEntryDto>) {
        rows = data
        fireTableDataChanged()
    }

    override fun getRowCount(): Int = rows.size

    override fun getColumnCount(): Int = columns.size

    override fun getColumnName(column: Int): String = columns[column]

    override fun getValueAt(rowIndex: Int, columnIndex: Int): Any {
        val entry = rows[rowIndex]
        return when (columnIndex) {
            0 -> entry.timestamp.substringAfter('T').substringBefore('.').ifBlank { entry.timestamp }
            1 -> entry.kind
            2 -> entry.label
            3 -> entry.durationMs?.let { "%.0f".format(it) } ?: "—"
            4 -> entry.traceId ?: entry.correlationId ?: entry.requestPath ?: "—"
            else -> entry.eventId?.take(8) ?: "—"
        }
    }
}

private class TracesTableModel : AbstractTableModel() {
    private val columns = arrayOf("Trace key", "Events", "Slow", "Failures", "Total ms", "TraceId")
    private var rows: List<QueryDuckTraceGroupDto> = emptyList()

    fun setRows(data: List<QueryDuckTraceGroupDto>) {
        rows = data
        fireTableDataChanged()
    }

    override fun getRowCount(): Int = rows.size

    override fun getColumnCount(): Int = columns.size

    override fun getColumnName(column: Int): String = columns[column]

    override fun getValueAt(rowIndex: Int, columnIndex: Int): Any {
        val group = rows[rowIndex]
        return when (columnIndex) {
            0 -> group.traceKey
            1 -> group.eventCount
            2 -> group.slowQueryCount
            3 -> group.failureCount
            4 -> "%.0f".format(group.totalDurationMs)
            else -> group.traceId ?: group.correlationId ?: group.requestPath ?: "—"
        }
    }
}

private class StatementCacheTableModel : AbstractTableModel() {
    private val columns = arrayOf("Signature", "Variants", "Message")
    private var rows: List<StatementCacheFindingDto> = emptyList()

    fun setRows(data: List<StatementCacheFindingDto>) {
        rows = data
        fireTableDataChanged()
    }

    override fun getRowCount(): Int = rows.size

    override fun getColumnCount(): Int = columns.size

    override fun getColumnName(column: Int): String = columns[column]

    override fun getValueAt(rowIndex: Int, columnIndex: Int): Any =
        when (columnIndex) {
            0 -> rows[rowIndex].signature
            1 -> rows[rowIndex].variantCount
            else -> rows[rowIndex].message
        }
}
