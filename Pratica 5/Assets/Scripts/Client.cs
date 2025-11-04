using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Collections.Concurrent;

public class Client : MonoBehaviour
{
    public int myId = -1;
    private UdpClient client;
    private Thread receiveThread;
    private IPEndPoint serverEP;

    public GameObject[] players = new GameObject[4]; // Referência aos 4 jogadores (duplas)
    private Vector3[] remotePositions = new Vector3[4];

    public GameObject bola;
    public int Velocidade = 20;
    private bool jogoTerminado = false; // Flag para impedir movimento após vitória

    // Nova variável para interpolação da bola (como os jogadores)
    private Vector3 remoteBallPosition = Vector3.zero;

    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    void Start()
    {
        client = new UdpClient();
        serverEP = new IPEndPoint(IPAddress.Parse("10.57.1.76"), 5001); // IP do servidor
        client.Connect(serverEP);

        receiveThread = new Thread(ReceiveData);
        receiveThread.Start();

        // Solicita conexão
        client.Send(Encoding.UTF8.GetBytes("HELLO"), 5);

        // Bola inicial - garante visibilidade
        if (bola != null)
        {
            bola.transform.position = Vector3.zero;
            var rb = bola.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            remoteBallPosition = Vector3.zero;

            // Garante que a bola seja visível (ativa SpriteRenderer se existir)
            var renderer = bola.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
                Debug.Log($"[Cliente] Bola visível ativada para ID {myId}");
            }
            else
            {
                Debug.LogWarning("[Cliente] SpriteRenderer não encontrado na bola!");
            }
        }
        else
        {
            Debug.LogError("[Cliente] GameObject 'bola' não atribuído no Inspector!");
        }
    }

    void Update()
    {
        // Processa mensagens da thread
        while (messageQueue.TryDequeue(out string msg))
        {
            ProcessMessage(msg);
        }

        if (myId == -1 || jogoTerminado) return; // Não move se o jogo acabou

        // Movimento do jogador local (em duplas, cada um controla sua raquete)
        float v = Input.GetAxis("Vertical");
        if (players[myId - 1] != null)
        {
            players[myId - 1].transform.Translate(new Vector3(0, v, 0) * Time.deltaTime * Velocidade);

            // Limites
            Vector3 pos = players[myId - 1].transform.position;
            pos.y = Mathf.Clamp(pos.y, -3f, 3f);
            players[myId - 1].transform.position = pos;

            // Envia posição
            string msgPos =
                $"POS:{myId};{pos.x.ToString("F2", CultureInfo.InvariantCulture)};{pos.y.ToString("F2", CultureInfo.InvariantCulture)}";
            SendUdpMessage(msgPos);
        }

        // Atualiza as posições dos outros jogadores
        for (int i = 0; i < 4; i++)
        {
            if (i != (myId - 1) && players[i] != null)
            {
                players[i].transform.position = Vector3.Lerp(
                    players[i].transform.position,
                    remotePositions[i],
                    Time.deltaTime * 10f
                );
            }
        }

        // Interpola a posição da bola para suavidade (como os jogadores)
        if (bola != null)
        {
            bola.transform.position = Vector3.Lerp(
                bola.transform.position,
                remoteBallPosition,
                Time.deltaTime * 10f // Mesmo fator de interpolação
            );
        }
    }

    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            byte[] data = client.Receive(ref remoteEP);
            string msg = Encoding.UTF8.GetString(data);
            messageQueue.Enqueue(msg);
        }
    }

    // ReSharper disable Unity.PerformanceAnalysis
    void ProcessMessage(string msg)
    {
        if (msg.StartsWith("ASSIGN:"))
        {
            myId = int.Parse(msg.Substring(7));
            Debug.Log($"[Cliente] ID atribuído: {myId} (duplas: Time A=1-2, Time B=3-4)");

            // Define posições iniciais para duplas (uma na frente da outra)
            Vector3[] startPositions = new Vector3[]
            {
                new Vector3(-8f, 0f, -1f), // Player 1 - Time A (frente)
                new Vector3(-8f, 0f, 1f),  // Player 2 - Time A (atrás)
                new Vector3(8f, 0f, -1f),  // Player 3 - Time B (frente)
                new Vector3(8f, 0f, 1f)    // Player 4 - Time B (atrás)
            };
            

            for (int i = 0; i < 4; i++)
            {
                players[i] = GameObject.Find("Player " + (i + 1));
                if (players[i] != null)
                {
                    players[i].transform.position = startPositions[i];
                    remotePositions[i] = startPositions[i];
                    // Ajusta ordem de renderização (para quem aparece na frente)
                    var rend = players[i].GetComponent<SpriteRenderer>();
                    if (rend != null)
                    {
                        rend.sortingOrder = (i % 2 == 0) ? 2 : 1; // Primeiro de cada time na frente
                    }

                }
            }

            // Reset bola
            if (bola != null)
            {
                bola.transform.position = Vector3.zero;
                var rb = bola.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.linearVelocity = Vector2.zero;
                remoteBallPosition = Vector3.zero;
            }
        }
        else if (msg.StartsWith("POS:"))
        {
            string[] parts = msg.Substring(4).Split(';');
            if (parts.Length == 3)
            {
                int id = int.Parse(parts[0]);
                if (id >= 1 && id <= 4 && id != myId)
                {
                    float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                    remotePositions[id - 1] = new Vector3(x, y, 0);
                }
            }
        }
        else if (msg.StartsWith("BALL:"))
        {
            // Bola sincronizada entre duplas com interpolação
            string[] parts = msg.Substring(5).Split(';');
            if (parts.Length == 2)
            {
                float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
                float y = float.Parse(parts[1], CultureInfo.InvariantCulture);
                remoteBallPosition = new Vector3(x, y, 0);
                // Debug.Log($"[Cliente {myId}] Recebeu BALL: {remoteBallPosition}"); // Descomente se quiser logs constantes
            }
        }
        else if (msg.StartsWith("SCORE:"))
        {
            string[] parts = msg.Substring(6).Split(';');
            if (parts.Length == 2 && bola != null)
            {
                int scoreA = int.Parse(parts[0]);
                int scoreB = int.Parse(parts[1]);

                var bolaScript = bola.GetComponent<Bola>();
                bolaScript.PontoTimeA = scoreA;
                bolaScript.PontoTimeB = scoreB;
                bolaScript.textoPontoA.text = "Pontos: " + scoreA;
                bolaScript.textoPontoB.text = "Pontos: " + scoreB;
            }
        }
        else if (msg == "GAMEOVER")
        {
            jogoTerminado = true; // Para movimento local
            Debug.Log("[Cliente] Jogo terminado!");
        }
    }

    public void SendUdpMessage(string msg)
    {
        client.Send(Encoding.UTF8.GetBytes(msg), msg.Length);
    }

    void OnApplicationQuit()
    {
        receiveThread?.Abort();
        client?.Close();
    }
}