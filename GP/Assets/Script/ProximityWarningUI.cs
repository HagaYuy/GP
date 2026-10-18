
using UnityEngine;

public class ProximityWarningUI : MonoBehaviour
{
    [Header("警告UI")]
    [SerializeField] private RectTransform warningUI;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private UnityEngine.Camera targetCamera;

    [Header("警告範囲")]
    [SerializeField] private float warningDistance = 25f;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("警告設定")]
    [SerializeField] private bool onlyWarnForObstaclesAhead = true;
    [SerializeField] private float checkInterval = 0.05f;

    [Header("画面端からの余白")]
    [SerializeField] private float screenMargin = 60f;

    private float checkTimer;
    private Collider currentObstacle;
    private Rigidbody playerRigidbody;
    private Canvas warningCanvas;
    private UnityEngine.Camera canvasCamera;

    private void Awake()
    {
        playerRigidbody = GetComponent<Rigidbody>();

        if (targetCamera == null)
        {
            targetCamera = UnityEngine.Camera.main;
        }

        if (warningUI != null)
        {
            warningCanvas = warningUI.GetComponentInParent<Canvas>();
        }

        if (warningCanvas != null && canvasRect == null)
        {
            canvasRect = warningCanvas.GetComponent<RectTransform>();
        }

        if (warningUI == null ||
            warningCanvas == null ||
            canvasRect == null ||
            targetCamera == null)
        {
            Debug.LogError(
                "WarningUI、Canvas、TargetCameraの設定を確認してください。"
            );

            enabled = false;
            return;
        }

        // Canvasの描画方式に合わせて座標変換用カメラを決める
        if (warningCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            canvasCamera = null;
        }
        else
        {
            canvasCamera = warningCanvas.worldCamera != null
                ? warningCanvas.worldCamera
                : targetCamera;
        }

        // アイコンの基準点を中央に統一
        warningUI.anchorMin = new Vector2(0.5f, 0.5f);
        warningUI.anchorMax = new Vector2(0.5f, 0.5f);
        warningUI.pivot = new Vector2(0.5f, 0.5f);

        warningUI.gameObject.SetActive(false);
    }

    private void Update()
    {
        checkTimer -= Time.deltaTime;

        // 一定間隔で障害物を検索
        if (checkTimer <= 0f)
        {
            checkTimer = Mathf.Max(checkInterval, 0.01f);
            currentObstacle = FindNearestObstacle();
        }

        if (currentObstacle == null)
        {
            if (warningUI.gameObject.activeSelf)
            {
                warningUI.gameObject.SetActive(false);
            }

            return;
        }

        if (!warningUI.gameObject.activeSelf)
        {
            warningUI.gameObject.SetActive(true);
        }

        // 表示位置は毎フレーム更新する
        UpdateWarningPosition(currentObstacle);
    }

    private Collider FindNearestObstacle()
    {
        Collider[] obstacles = Physics.OverlapSphere(
            transform.position,
            warningDistance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore
        );

        Collider nearestObstacle = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider obstacle in obstacles)
        {
            if (obstacle == null)
            {
                continue;
            }

            // プレイヤー自身のColliderは無視
            if (obstacle.transform == transform ||
                obstacle.transform.IsChildOf(transform) ||
                (playerRigidbody != null &&
                 obstacle.attachedRigidbody == playerRigidbody))
            {
                continue;
            }

            Vector3 toObstacle =
                obstacle.bounds.center - transform.position;

            // プレイヤーの後方にある障害物は無視
            if (onlyWarnForObstaclesAhead &&
                Vector3.Dot(transform.forward, toObstacle) <= 0f)
            {
                continue;
            }

            // Colliderの表面までの距離を計算
            Vector3 closestPoint =
                obstacle.ClosestPoint(transform.position);

            float distance = Vector3.Distance(
                transform.position,
                closestPoint
            );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestObstacle = obstacle;
            }
        }

        return nearestObstacle;
    }

    private void UpdateWarningPosition(Collider obstacle)
    {
        if (obstacle == null)
        {
            return;
        }

        // ① 障害物のワールド座標をカメラの画面座標に変換
        Vector3 obstacleScreen =
            targetCamera.WorldToScreenPoint(obstacle.bounds.center);

        Vector2 screenCenter = new Vector2(
            Screen.width * 0.5f,
            Screen.height * 0.5f
        );

        Vector2 obstacleScreen2D = new Vector2(
            obstacleScreen.x,
            obstacleScreen.y
        );

        // ② カメラの後方にある場合は方向を反転
        bool behindCamera = obstacleScreen.z < 0f;

        if (behindCamera)
        {
            obstacleScreen2D = screenCenter * 2f - obstacleScreen2D;
        }

        // ③ 画面中央と障害物の位置をCanvasのローカル座標に変換
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenCenter,
            canvasCamera,
            out Vector2 centerLocal))
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            obstacleScreen2D,
            canvasCamera,
            out Vector2 obstacleLocal))
        {
            return;
        }

        // ④ 画面中央から障害物に向かう方向を求める
        Vector2 direction = obstacleLocal - centerLocal;

        // 障害物が画面中央に重なっている場合
        // 正面なら上、カメラの後方なら下を初期方向とする
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = behindCamera ? Vector2.down : Vector2.up;
        }

        direction.Normalize();

        // ⑤ アイコンが収まる画面端の範囲を計算
        Rect canvasBounds = canvasRect.rect;

        float halfWidth =
            Mathf.Min(
                centerLocal.x - canvasBounds.xMin,
                canvasBounds.xMax - centerLocal.x
            )
            - screenMargin
            - warningUI.rect.width * 0.5f;

        float halfHeight =
            Mathf.Min(
                centerLocal.y - canvasBounds.yMin,
                canvasBounds.yMax - centerLocal.y
            )
            - screenMargin
            - warningUI.rect.height * 0.5f;

        if (halfWidth <= 0f || halfHeight <= 0f)
        {
            return;
        }

        // ⑥ 障害物の方向に沿って画面端までの距離を求める
        float scaleX = Mathf.Abs(direction.x) > 0.0001f
            ? halfWidth / Mathf.Abs(direction.x)
            : float.PositiveInfinity;

        float scaleY = Mathf.Abs(direction.y) > 0.0001f
            ? halfHeight / Mathf.Abs(direction.y)
            : float.PositiveInfinity;

        float scale = Mathf.Min(scaleX, scaleY);

        Vector2 targetLocalPosition =
            centerLocal + direction * scale;

        // Canvas上の座標をワールド座標に変換してアイコンを配置
        // warningUIがCanvasの直接の子でなくても位置を合わせられる
        warningUI.position =
            canvasRect.TransformPoint(targetLocalPosition);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            warningDistance
        );
    }
}
