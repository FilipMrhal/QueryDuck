plugins {
    id("java")
    id("org.jetbrains.kotlin.jvm") version "2.1.10"
    id("org.jetbrains.intellij.platform") version "2.3.0"
}

group = "com.queryduck"
version = "1.4.0"

repositories {
    mavenCentral()
    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        rider("2025.3", useInstaller = false)
        jetbrainsRuntime()
    }
    implementation("com.google.code.gson:gson:2.11.0")
}

intellijPlatform {
    pluginConfiguration {
        id = "com.queryduck.rider"
        name = "QueryDuck"
        version = project.version.toString()
        description = "Live EF Core query debugger with SQL highlighting, expression trees, diagnostics, and plans"
        vendor {
            name = "QueryDuck"
        }
    }
}

kotlin {
    jvmToolchain(21)
}

tasks {
    buildSearchableOptions {
        enabled = false
    }
}
