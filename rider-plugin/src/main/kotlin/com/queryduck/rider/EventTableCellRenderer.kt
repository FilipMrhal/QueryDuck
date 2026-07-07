package com.queryduck.rider

import java.awt.Color
import java.awt.Component
import java.awt.Font
import javax.swing.JTable
import javax.swing.table.DefaultTableCellRenderer

class EventTableCellRenderer : DefaultTableCellRenderer() {
    override fun getTableCellRendererComponent(
        table: JTable?,
        value: Any?,
        isSelected: Boolean,
        hasFocus: Boolean,
        row: Int,
        column: Int,
    ): Component {
        val component = super.getTableCellRendererComponent(table, value, isSelected, hasFocus, row, column)
        if (!isSelected && column == 3 && value is String && value != "·") {
            foreground = Color(0xE8A035)
            font = font.deriveFont(Font.BOLD)
        }
        return component
    }
}
