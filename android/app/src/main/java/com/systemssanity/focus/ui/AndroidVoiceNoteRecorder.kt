package com.systemssanity.focus.ui

import android.content.Context
import android.media.MediaRecorder
import com.systemssanity.focus.domain.maps.VoiceNotes
import java.io.File

internal class AndroidVoiceNoteRecorder(private val context: Context) {
    private var recorder: MediaRecorder? = null
    private var outputFile: File? = null

    fun start(): Result<Unit> =
        runCatching {
            cleanup()
            val file = File.createTempFile("focus-voice-", VoiceNotes.Extension, context.cacheDir)
            val nextRecorder = createRecorder()
            try {
                nextRecorder.apply {
                    setAudioSource(MediaRecorder.AudioSource.MIC)
                    setOutputFormat(MediaRecorder.OutputFormat.MPEG_4)
                    setAudioEncoder(MediaRecorder.AudioEncoder.AAC)
                    setAudioEncodingBitRate(128_000)
                    setAudioSamplingRate(44_100)
                    setOutputFile(file.absolutePath)
                    prepare()
                    start()
                }
            } catch (error: Throwable) {
                nextRecorder.release()
                file.delete()
                throw error
            }
            outputFile = file
            recorder = nextRecorder
        }.onFailure {
            cleanup()
        }

    fun stopAndSave(): Result<File> =
        runCatching {
            val file = outputFile ?: error("Voice recording was empty. Try recording again.")
            val currentRecorder = recorder ?: error("Voice recording is not active.")
            try {
                currentRecorder.stop()
            } finally {
                releaseRecorder()
            }
            if (!file.isFile || file.length() <= 0L) {
                file.delete()
                error("Voice recording was empty. Try recording again.")
            }
            outputFile = null
            file
        }.onFailure {
            cleanup()
        }

    fun cancel() {
        runCatching {
            recorder?.stop()
        }
        cleanup()
    }

    fun cleanup() {
        releaseRecorder()
        outputFile?.delete()
        outputFile = null
    }

    private fun releaseRecorder() {
        recorder?.release()
        recorder = null
    }

    @Suppress("DEPRECATION")
    private fun createRecorder(): MediaRecorder =
        MediaRecorder()
}
