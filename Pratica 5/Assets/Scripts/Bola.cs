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

        // Começa coroutine que espera o ID ser atribuído
        StartCoroutine(EsperarHostDaBola());

        System.Collections.IEnumerator EsperarHostDaBola()
        {
            // Espera o Client existir e ter um ID válido
            while (udpClient == null || udpClient.myId == -1)
            {
                udpClient = FindObjectOfType<Client>();
                yield return null;
            }

            // Se este cliente NÃO for o host da bola (ID 4)
            if (udpClient.myId != 4)
            {
                rb.isKinematic = true;
                rb.simulated = false;
                Debug.Log($"[Cliente {udpClient.myId}] Física da bola desativada — aguardando sincronização via rede");
                yield break;
            }

            // Se for o host, ativa física e lança a bola
            rb.isKinematic = false;
            rb.simulated = true;
            Debug.Log("[Host] Sou o host da bola — lançando em 1 segundo...");
            yield return new WaitForSeconds(1f);
            LancarBola();
        }

    }

    private void LancarBola()
    {
        throw new System.NotImplementedException();
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

    public Bola()
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