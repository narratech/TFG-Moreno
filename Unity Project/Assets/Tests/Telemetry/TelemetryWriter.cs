using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;

public class TelemetryWriter : IDisposable
{
    private ConcurrentQueue<TelemetryFrame> _queue = new ConcurrentQueue<TelemetryFrame>();
    private Thread _writerThread;
    private bool _isRunning;
    private StreamWriter _streamWriter;
    private bool _isFirstEntry = true;

    public TelemetryWriter(string fileName)
    {
        // 1. Definir la carpeta Metrics dentro de Assets (o directorio del proyecto)
        string folderPath = Path.Combine(Application.dataPath, "Tests", "Metrics", "data");

        // 2. Crear la carpeta si aún no existe
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // 3. Ruta final completa del archivo
        string path = Path.Combine(folderPath, fileName);

        _streamWriter = new StreamWriter(path, false);
        _streamWriter.WriteLine("["); // Inicia el array JSON

        _isRunning = true;
        _writerThread = new Thread(WriteLoop)
        {
            IsBackground = true, // Asegura que el hilo muera si Unity crashea
            Name = "TelemetryWriterThread"
        };
        _writerThread.Start();

        Debug.Log($"[TelemetryTracker] Hilo de escritura iniciado. Guardando en: {path}");
    }

    public void EnqueueFrame(TelemetryFrame frame)
    {
        _queue.Enqueue(frame);
    }

    private void WriteLoop()
    {
        while (_isRunning || !_queue.IsEmpty)
        {
            if (_queue.TryDequeue(out TelemetryFrame frame))
            {
                string json = JsonUtility.ToJson(frame, true);

                if (!_isFirstEntry)
                {
                    _streamWriter.WriteLine(",");
                }

                _streamWriter.Write(json);
                _isFirstEntry = false;
            }
            else
            {
                Thread.Sleep(10); // Evita consumir CPU si la cola está vacía
            }
        }
    }

    public void Dispose()
    {
        if (!_isRunning) return; // Evitar doble dispose

        _isRunning = false;

        // Esperamos a que el hilo termine de vaciar la cola
        if (_writerThread != null && _writerThread.IsAlive)
        {
            _writerThread.Join();
        }

        if (_streamWriter != null)
        {
            _streamWriter.WriteLine("\n]"); // Cierra el array JSON
            _streamWriter.Close();
            _streamWriter.Dispose();
        }
    }
}