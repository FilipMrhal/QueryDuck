package com.queryduck.rider

import com.intellij.openapi.ide.CopyPasteManager
import com.intellij.openapi.project.Project
import com.intellij.openapi.editor.ex.EditorEx
import com.intellij.openapi.fileTypes.PlainTextFileType
import com.intellij.lang.Language
import com.intellij.openapi.fileTypes.LanguageFileType
import com.intellij.ui.EditorTextField
import com.intellij.util.ui.JBUI
import java.awt.BorderLayout
import java.awt.datatransfer.StringSelection
import javax.swing.JButton
import javax.swing.JPanel

class QueryDuckCodeEditor(
    private val project: Project,
    languageId: String,
) : JPanel(BorderLayout()) {
    private val fileType: LanguageFileType = resolveFileType(languageId)
    private val editorField = EditorTextField("", project, fileType).apply {
        isOneLineMode = false
        addSettingsProvider { editor ->
            (editor as EditorEx).apply {
                isViewer = true
                settings.apply {
                    isLineNumbersShown = true
                    isCaretRowShown = false
                    isUseSoftWraps = true
                    additionalLinesCount = 2
                }
            }
        }
    }

    init {
        border = JBUI.Borders.empty()
        add(editorField, BorderLayout.CENTER)
    }

    fun setText(text: String) {
        editorField.text = text
    }

    fun getText(): String = editorField.text

    companion object {
        fun sqlPanel(project: Project): TitledCodeEditorPanel =
            TitledCodeEditorPanel(project, "SQL", "SQL")

        fun csharpPanel(project: Project): TitledCodeEditorPanel =
            TitledCodeEditorPanel(project, "C#", "C#")

        fun planPanel(project: Project): TitledCodeEditorPanel =
            TitledCodeEditorPanel(project, "Plan", "SQL")

        private fun resolveFileType(languageId: String): LanguageFileType {
            val language = Language.findLanguageByID(languageId)
            val associated = language?.associatedFileType
            if (associated is LanguageFileType) {
                return associated
            }

            return PlainTextFileType.INSTANCE
        }
    }
}

class TitledCodeEditorPanel(
    project: Project,
    private val copyLabel: String,
    languageId: String,
) : JPanel(BorderLayout()) {
    private val editor = QueryDuckCodeEditor(project, languageId)

    init {
        add(buildHeader(copyLabel), BorderLayout.NORTH)
        add(editor, BorderLayout.CENTER)
    }

    fun setText(text: String) = editor.setText(text)

    fun getText(): String = editor.getText()

    private fun buildHeader(label: String): JPanel =
        JPanel(BorderLayout()).apply {
            border = JBUI.Borders.empty(0, 0, 4, 0)
            add(
                JButton("Copy $label").apply {
                    addActionListener {
                        CopyPasteManager.getInstance().setContents(StringSelection(editor.getText()))
                    }
                },
                BorderLayout.EAST,
            )
        }
}
