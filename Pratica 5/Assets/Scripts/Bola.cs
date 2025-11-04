using UnityEngine;
using TMPro;
using System.Collections;

public class Bola : MonoBehaviour
{
    private Rigidbody2D rb;
    private Client udpClient;
    private bool bolaLancada = false;
    private bool jogoTerminado = false;

    [Header("Pontuação")]
    public int PontoTimeA = 0;
    public int PontoTimeB = 0;
    public TextMeshProUGUI textoPontoA;
    public TextMeshProUGUI textoPontoB;
    public TextMeshProUGUI VitoriaLocal;
    public TextMeshProUGUI VitoriaRemote;

    [Header("Configuração da Bola")]
    public float velocidade = 5f;
    public float fatorDesvio = 2f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        udpClient = FindObjectOfType<Client>();

        // Começa rotina que espera o ID ser atribuído
        StartCoroutine(EsperarHostDaBola());
    }

    private IEnumerator EsperarHostDaBola()
    {
        // Espera até o client existir e receber ID
        while (udpClient == null || udpClient.myId == -1)
        {
            yield return null;
        }

        Debug.Log($"[Bola] Meu jogador tem ID {udpClient.myId}");

        if (udpClient.myId != 4)
        {
            rb.isKinematic = true;
            rb.simulated = false;
            Debug.Log($"[Cliente {udpClient.myId}] Física desativada — seguindo posição da rede.");
            yield break;
        }

        // Host: ativa física e lança
        rb.isKinematic = false;
        rb.simulated = true;
        Debug.Log("[Host] Sou o host da bola — lançando em 1 segundo...");
        yield return new WaitForSeconds(1f);
        LancarBola();
    }

    void Update()
    {
        if (udpClient == null || jogoTerminado) return;

        // Host envia posição
        if (udpClient.myId == 4)
        {
            string msg = "BALL:" +
                         transform.position.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";" +
                         transform.position.y.ToString(System.Globalization.CultureInfo.InvariantCulture);
            udpClient.SendUdpMessage(msg);
        }
    }

    void LancarBola()
    {
        if (jogoTerminado || bolaLancada) return;

        bolaLancada = true;
        float dirX = Random.Range(0, 2) == 0 ? -1 : 1;
        float dirY = Random.Range(-0.5f, 0.5f);
        rb.velocity = new Vector2(dirX, dirY).normalized * velocidade;

        Debug.Log($"[Host] Bola lançada com velocidade {rb.velocity}");
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (udpClient == null || jogoTerminado) return;

        if (col.gameObject.CompareTag("Raquete"))
        {
            float posYbola = transform.position.y;
            float posYraquete = col.transform.position.y;
            float alturaRaquete = col.collider.bounds.size.y;

            float diferenca = (posYbola - posYraquete) / (alturaRaquete / 2f);
            Vector2 direcao = new Vector2(Mathf.Sign(rb.velocity.x), diferenca * fatorDesvio);
            rb.velocity = direcao.normalized * velocidade;
        }
        else if (col.gameObject.CompareTag("Gol1"))
        {
            PontoTimeB++;
            textoPontoB.text = "Pontos: " + PontoTimeB;
            ResetBola();
        }
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
        rb.velocity = Vector2.zero;
        bolaLancada = false;

        if (PontoTimeA >= 5 || PontoTimeB >= 5)
        {
            GameOver();
        }
        else if (udpClient != null && udpClient.myId == 4)
        {
            Invoke("LancarBola", 1f);
            string msg = "SCORE:" + PontoTimeA + ";" + PontoTimeB;
            udpClient.SendUdpMessage(msg);
        }
    }

    void GameOver()
    {
        jogoTerminado = true;
        transform.position = Vector3.zero;
        rb.velocity = Vector2.zero;

        if (udpClient != null && udpClient.myId == 4)
            udpClient.SendUdpMessage("GAMEOVER");
    }
}
