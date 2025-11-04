using UnityEngine;
using TMPro;

public class Bola : MonoBehaviour
{
    private Rigidbody2D rb;
    private Client udpClient;
    private bool bolaLancada = false;
    private bool jogoTerminado = false; // Flag para impedir ações após vitória

    [Header("Pontuação")]
    public int PontoTimeA = 0;
    public int PontoTimeB = 0;
    public TextMeshProUGUI textoPontoA;
    public TextMeshProUGUI textoPontoB;
    public TextMeshProUGUI VitoriaLocal;
    public TextMeshProUGUI VitoriaRemote;

    [Header("Configuração da Bola")]
    public float velocidade = 5f;   // Velocidade base da bola
    public float fatorDesvio = 2f;  // Quanto o ponto de contato influencia o ângulo

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        udpClient = FindObjectOfType<Client>();

        // Desabilita física nos clientes não-host para evitar conflitos
        if (udpClient != null && udpClient.myId != 4)
        {
            rb.isKinematic = true;  // Bola segue apenas posições recebidas via rede
            rb.simulated = false;   // Desabilita simulação física
        }

        // O jogador com ID 4 será o "host da bola" (responsável por lançá-la e sincronizar)
        if (udpClient != null && udpClient.myId == 4 && !jogoTerminado)
        {
            Debug.Log("[Host] Tentando lançar bola em 1 segundo...");
            Invoke("LancarBola", 1f);
        }
        else
        {
            Debug.Log($"[Cliente {udpClient?.myId}] Não sou o host ou jogo terminou, não lanço bola.");
        }
    }

    void Update()
    {
        if (udpClient == null || jogoTerminado) return;

        // Host (ID 4) controla e envia posição da bola (mesmo parada, para sincronização inicial)
        if (udpClient.myId == 4)
        {
            string msg = "BALL:" +
                         transform.position.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";" +
                         transform.position.y.ToString(System.Globalization.CultureInfo.InvariantCulture);

            udpClient.SendUdpMessage(msg);
            // Debug.Log($"[Host] Enviando BALL: {transform.position}"); // Descomente se quiser logs constantes
        }
    }

    void LancarBola()
    {
        if (jogoTerminado || bolaLancada) return; // Não lança se o jogo acabou ou já lançou
        bolaLancada = true;
        float dirX = Random.Range(0, 2) == 0 ? -1 : 1;
        float dirY = Random.Range(-0.5f, 0.5f);
        rb.linearVelocity = new Vector2(dirX, dirY).normalized * velocidade;
        Debug.Log($"[Host] Bola lançada com velocidade: {rb.linearVelocity}");
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (udpClient == null || jogoTerminado) return; // Todos processam colisões para consistência

        // Rebote nas raquetes (cada jogador controla sua própria raquete em duplas)
        if (col.gameObject.CompareTag("Raquete"))
        {
            float posYbola = transform.position.y;
            float posYraquete = col.transform.position.y;
            float alturaRaquete = col.collider.bounds.size.y;

            float diferenca = (posYbola - posYraquete) / (alturaRaquete / 2f);

            Vector2 direcao = new Vector2(Mathf.Sign(rb.linearVelocity.x), diferenca * fatorDesvio);
            rb.linearVelocity = direcao.normalized * velocidade;
        }
        // Gol na esquerda (Time B marca ponto)
        else if (col.gameObject.CompareTag("Gol1"))
        {
            PontoTimeB++;
            textoPontoB.text = "Pontos: " + PontoTimeB;
            ResetBola();
        }
        // Gol na direita (Time A marca ponto)
        else if (col.gameObject.CompareTag("Gol2"))
        {
            PontoTimeA++;
            textoPontoA.text = "Pontos: " + PontoTimeA;
            ResetBola();
        }
    }

    void ResetBola()
    {
        transform.position = Vector3.zero;
        rb.linearVelocity = Vector2.zero;
        bolaLancada = false; // Permite relançar

        if (PontoTimeA >= 5 || PontoTimeB >= 5) // Limite ajustado para 5 pontos em duplas
        {
            GameOver();
        }
        // Apenas o host (ID 4) envia novo placar e relança a bola
        else if (udpClient != null && udpClient.myId == 4)
        {
            Invoke("LancarBola", 1f);

            string msg = "SCORE:" + PontoTimeA + ";" + PontoTimeB;
            udpClient.SendUdpMessage(msg);
        }
    }

    void GameOver()
    {
        jogoTerminado = true; // Para o jogo
        transform.position = Vector3.zero;
        rb.linearVelocity = Vector2.zero;

        // Notifica o servidor para retransmitir GAMEOVER
        if (udpClient != null && udpClient.myId == 4)
        {
            udpClient.SendUdpMessage("GAMEOVER");
        }

        // Mostra mensagem de vitória para cada time
        if (PontoTimeA >= 5 && (udpClient.myId == 1 || udpClient.myId == 2))
        {
            VitoriaLocal.gameObject.SetActive(true);
        }
        else if (PontoTimeA >= 5 && (udpClient.myId == 3 || udpClient.myId == 4))
        {
            VitoriaRemote.gameObject.SetActive(true);
        }
        else if (PontoTimeB >= 5 && (udpClient.myId == 1 || udpClient.myId == 2))
        {
            VitoriaRemote.gameObject.SetActive(true);
        }
        else if (PontoTimeB >= 5 && (udpClient.myId == 3 || udpClient.myId == 4))
        {
            VitoriaLocal.gameObject.SetActive(true);
        }
        else
        {
            // Caso de empate (improvável, mas para segurança)
            VitoriaLocal.text = "Empate!";
            VitoriaLocal.gameObject.SetActive(true);
        }
    }
}