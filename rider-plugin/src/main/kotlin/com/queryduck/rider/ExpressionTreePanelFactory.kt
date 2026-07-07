package com.queryduck.rider

import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.treeStructure.Tree
import com.intellij.util.ui.JBUI
import java.awt.BorderLayout
import java.awt.Font
import javax.swing.JPanel
import javax.swing.tree.DefaultMutableTreeNode
import javax.swing.tree.DefaultTreeModel

object ExpressionTreePanelFactory {
    fun createPanel(event: QueryCaptureEventDto?): JPanel {
        val panel = JPanel(BorderLayout())
        panel.border = JBUI.Borders.empty(4)

        if (event == null) {
            panel.add(emptyLabel("Select a captured query to inspect its expression tree."), BorderLayout.CENTER)
            return panel
        }

        val rootNode = event.expressionTree?.let { buildTreeNode(it) }
            ?: buildTextFallbackNode(event.expressionTreeText)

        val tree = Tree(DefaultTreeModel(rootNode)).apply {
            isRootVisible = true
            showsRootHandles = true
            font = Font(Font.MONOSPACED, Font.PLAIN, 12)
            expandRow(0)
        }

        panel.add(JBScrollPane(tree), BorderLayout.CENTER)
        return panel
    }

    private fun buildTreeNode(dto: ExpressionTreeNodeDto): DefaultMutableTreeNode {
        val label = buildString {
            append(dto.kind)
            append(" : ")
            append(dto.type)
            if (!dto.name.isNullOrBlank()) {
                append(" [")
                append(dto.name)
                append(']')
            }
            if (!dto.value.isNullOrBlank()) {
                append(" = ")
                append(dto.value)
            }
        }

        val node = DefaultMutableTreeNode(label)
        dto.children.orEmpty().forEach { child ->
            node.add(buildTreeNode(child))
        }
        return node
    }

    private fun buildTextFallbackNode(text: String?): DefaultMutableTreeNode {
        val content = text?.ifBlank { null } ?: "No structured expression tree was captured.\nEnable UseQueryDuckDebugging() to auto-capture all queries."
        val root = DefaultMutableTreeNode("Expression (text fallback)")
        content.lineSequence().forEach { line ->
            if (line.isNotBlank()) {
                root.add(DefaultMutableTreeNode(line.trim()))
            }
        }
        return root
    }

    private fun emptyLabel(message: String) =
        javax.swing.JLabel("<html><i>$message</i></html>").apply {
            border = JBUI.Borders.empty(8)
        }
}
