package com.systemssanity.focus

import android.app.Application
import com.systemssanity.focus.di.AppContainer

class FocusApplication : Application() {
    val appContainer: AppContainer by lazy {
        AppContainer(this)
    }
}
