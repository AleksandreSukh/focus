package com.systemssanity.focus

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import com.systemssanity.focus.ui.FocusApp
import com.systemssanity.focus.ui.NativeRouteRequest
import com.systemssanity.focus.ui.nativeRouteRequestFromUri

class MainActivity : ComponentActivity() {
    private var routeRequestVersion = 0L
    private var routeRequest by mutableStateOf<NativeRouteRequest?>(null)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        updateRouteRequest(intent)
        setContent {
            FocusApp(routeRequest = routeRequest)
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        updateRouteRequest(intent)
    }

    private fun updateRouteRequest(intent: Intent?) {
        routeRequestVersion += 1
        routeRequest = nativeRouteRequestFromUri(intent?.dataString, routeRequestVersion)
    }
}
