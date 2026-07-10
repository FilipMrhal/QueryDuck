package com.queryduck.rider

import com.intellij.ide.util.PropertiesComponent

object QueryDuckSettings {
    private const val SERVER_URL_KEY = "queryduck.rider.serverUrl"

    fun loadServerUrl(defaultValue: String = DEFAULT_SERVER_URL): String =
        PropertiesComponent.getInstance().getValue(SERVER_URL_KEY, defaultValue)

    fun saveServerUrl(url: String) {
        PropertiesComponent.getInstance().setValue(SERVER_URL_KEY, url)
    }

    const val DEFAULT_SERVER_URL = "http://127.0.0.1:17654"
}
