package com.queryduck.rider

import com.intellij.ui.JBSplitter
import com.intellij.ui.components.JBLabel
import com.intellij.util.ui.JBUI
import java.awt.BorderLayout
import java.awt.Color
import java.awt.Dimension
import java.awt.Font
import java.awt.Graphics
import java.awt.Graphics2D
import java.awt.RenderingHints
import javax.swing.BorderFactory
import javax.swing.JPanel
import javax.swing.JScrollPane

object PlanFlowGraphPanelFactory {
    fun createSideBySidePanel(
        project: com.intellij.openapi.project.Project,
        planDiff: PlanDiffVisualizationDto?,
    ): JPanel {
        if (planDiff == null) {
            return JPanel(BorderLayout()).apply {
                add(
                    JBLabel("Enable EmitMermaidPlanGraphs in QueryDuck options to include plan graphs."),
                    BorderLayout.CENTER,
                )
            }
        }

        val hasMermaid = !planDiff.originalMermaid.isNullOrBlank() || !planDiff.improvedMermaid.isNullOrBlank()
        val hasSteps = planDiff.originalSteps.isNotEmpty() || planDiff.improvedSteps.isNotEmpty()
        if (!hasMermaid && !hasSteps) {
            return JPanel(BorderLayout()).apply {
                add(JBLabel("No plan graph data in this event."), BorderLayout.CENTER)
            }
        }

        return JPanel(BorderLayout()).apply {
            if (!planDiff.sideBySideMermaid.isNullOrBlank()) {
                add(
                    JBLabel(" Combined Mermaid (paste into mermaid.live to preview) ").apply {
                        border = JBUI.Borders.empty(4, 8)
                        foreground = Color(0x8FA2C3)
                    },
                    BorderLayout.NORTH,
                )
            }

            val splitter = JBSplitter(false, 0.5f).apply {
                firstComponent = buildColumn(
                    project,
                    "Original plan",
                    planDiff.originalSteps,
                    planDiff.originalMermaid,
                )
                secondComponent = buildColumn(
                    project,
                    "Improved plan",
                    planDiff.improvedSteps,
                    planDiff.improvedMermaid,
                )
            }
            add(splitter, BorderLayout.CENTER)

            if (!planDiff.sideBySideMermaid.isNullOrBlank()) {
                val combined = QueryDuckCodeEditor.planPanel(project).apply {
                    setText(planDiff.sideBySideMermaid)
                    border = BorderFactory.createTitledBorder("Combined Mermaid")
                }
                combined.preferredSize = Dimension(400, 120)
                add(combined, BorderLayout.SOUTH)
            }
        }
    }

    private fun buildColumn(
        project: com.intellij.openapi.project.Project,
        title: String,
        steps: List<PlanStepSummaryDto>,
        mermaid: String?,
    ): JPanel =
        JPanel(BorderLayout()).apply {
            border = BorderFactory.createTitledBorder(title)
            val graph = PlanStepGraphPanel(steps)
            graph.preferredSize = Dimension(280, 220)
            add(JScrollPane(graph), BorderLayout.CENTER)

            if (!mermaid.isNullOrBlank()) {
                val mermaidPanel = QueryDuckCodeEditor.planPanel(project).apply {
                    setText(mermaid)
                    border = BorderFactory.createTitledBorder("Mermaid")
                }
                mermaidPanel.preferredSize = Dimension(280, 140)
                add(mermaidPanel, BorderLayout.SOUTH)
            }
        }
}

private class PlanStepGraphPanel(
    private val steps: List<PlanStepSummaryDto>,
) : JPanel() {
    init {
        background = Color(0x1E1F22)
        border = JBUI.Borders.empty(8)
    }

    override fun paintComponent(g: Graphics) {
        super.paintComponent(g)
        val g2 = g as Graphics2D
        g2.setRenderingHint(RenderingHints.KEY_ANTIALIASING, RenderingHints.VALUE_ANTIALIAS_ON)

        if (steps.isEmpty()) {
            g2.color = Color(0x8FA2C3)
            g2.font = font.deriveFont(Font.ITALIC, 12f)
            g2.drawString("No steps", 12, 24)
            return
        }

        val boxWidth = (width - 40).coerceAtLeast(120)
        var y = 16
        steps.take(6).forEachIndexed { index, step ->
            val boxHeight = 52
            g2.color = if (index == 0) Color(0x3A4A6B) else Color(0x2B2D30)
            g2.fillRoundRect(12, y, boxWidth, boxHeight, 10, 10)
            g2.color = Color(0xDDE4F0)
            g2.font = font.deriveFont(Font.BOLD, 12f)
            g2.drawString(step.operation.take(28), 20, y + 18)
            g2.font = font.deriveFont(Font.PLAIN, 11f)
            g2.color = Color(0xA8B4CC)
            val detail = buildString {
                step.objectName?.let { append(it).append(" · ") }
                append("cost ")
                append(step.cost?.toInt()?.toString() ?: "?")
            }
            g2.drawString(detail.take(42), 20, y + 36)

            if (index < steps.size - 1 && index < 5) {
                val arrowX = 12 + boxWidth / 2
                g2.color = Color(0x6C7A96)
                g2.drawLine(arrowX, y + boxHeight, arrowX, y + boxHeight + 12)
                g2.drawLine(arrowX - 4, y + boxHeight + 8, arrowX, y + boxHeight + 12)
                g2.drawLine(arrowX + 4, y + boxHeight + 8, arrowX, y + boxHeight + 12)
            }

            y += boxHeight + 16
        }
    }

    override fun getPreferredSize(): Dimension {
        val count = steps.take(6).size.coerceAtLeast(1)
        return Dimension(280, 16 + count * 68)
    }
}
