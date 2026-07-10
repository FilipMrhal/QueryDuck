package com.queryduck.rider

import com.intellij.openapi.application.ApplicationManager
import java.util.concurrent.atomic.AtomicBoolean
import javax.swing.SwingUtilities

class QueryDuckBackgroundExecutor {
    private val inFlight = AtomicBoolean(false)

    fun runOnPooledThread(task: () -> Unit, onComplete: (() -> Unit)? = null) {
        if (!inFlight.compareAndSet(false, true)) {
            return
        }

        ApplicationManager.getApplication().executeOnPooledThread {
            try {
                task()
            } finally {
                inFlight.set(false)
                onComplete?.let { callback ->
                    SwingUtilities.invokeLater(callback)
                }
            }
        }
    }

    fun runOnPooledThreadWithResult(
        task: () -> Unit,
        onSuccess: () -> Unit,
        onError: (Exception) -> Unit,
    ) {
        if (!inFlight.compareAndSet(false, true)) {
            return
        }

        ApplicationManager.getApplication().executeOnPooledThread {
            try {
                task()
                SwingUtilities.invokeLater(onSuccess)
            } catch (ex: Exception) {
                SwingUtilities.invokeLater { onError(ex) }
            } finally {
                inFlight.set(false)
            }
        }
    }
}
