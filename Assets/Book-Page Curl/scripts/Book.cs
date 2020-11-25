using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Events;
public enum FlipMode
{
    RightToLeft,
    LeftToRight
}
static class TransformExtensions {
    public static void Reset(this Transform t)
    {
        t.localScale = Vector3.one;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        var rectTrans = t.GetComponent<RectTransform>();
        if(rectTrans!=null)
        {
            rectTrans.anchoredPosition = Vector2.zero;
        }
    }
}


public class Book : MonoBehaviour {
    public Canvas canvas;
    [SerializeField]
    RectTransform BookPanel;
    public GameObject PageStartItem;
    public GameObject PageEndItem;
    GameObject foreground;
    GameObject background;
    public GameObject[] bookPagesPrefabs;
    GameObject[] bookPages;
    public GameObject PageCache;
    public bool interactable=true;
    public bool enableShadowEffect=true;
    public bool moveExpirePageToCache =true;
    System.Func<int , GameObject> mGetPageItemByIndex;

    Vector3 touchStartPos = Vector3.zero;
    Vector3 touchEndPos = Vector3.zero;
    float touchStartTime = 0;
    public float flipForwardSpeed = 500;
    //represent the index of the sprite shown in the right page
    public int currentPage = 0;
    public int TotalPageCount
    {
        get { return bookPages.Length; }
    }
    public Vector3 EndBottomLeft
    {
        get { return ebl; }
    }
    public Vector3 EndBottomRight
    {
        get { return ebr; }
    }
    public float Height
    {
        get
        {
            return BookPanel.rect.height ; 
        }
    }
    public Image ClippingPlane;
    public Image NextPageClip;
    public Image Shadow;
    public Image ShadowLTR;
    public Image Left;
    public Image LeftNext;
    public Image Right;
    public Image RightNext;
    public UnityEvent OnFlip;
    public UnityEvent onGetPageItemByIndex;
    float radius1, radius2;
    //Spine Bottom
    Vector3 sb;
    //Spine Top
    Vector3 st;
    //corner of the page
    Vector3 c;
    //Edge Bottom Right
    Vector3 ebr;
    //Edge Bottom Left
    Vector3 ebl;
    //follow point 
    Vector3 f;
    bool pageDragging = false;
    public bool PageDragging
    {
        get
        {
            return pageDragging;
        }
    }
    //current flip mode
    FlipMode mode;

    public void Init(int pageCount, System.Func<int , GameObject> getPageItemByIndex)
    {
        PageCache.gameObject.SetActive(false);
        mGetPageItemByIndex = getPageItemByIndex;
    }

    public GameObject NewPageItem(string itemPrefabName)
    {
        //ItemPool pool = null;
        //if(mItemPoolDict.TryGetValue(itemPrefabName , out pool) == false)
        //{
        //    return null;
        //}
        //LoopListViewItem2 item = pool.GetItem();
        //RectTransform rf = item.GetComponent<RectTransform>();
        //rf.SetParent(mContainerTrans);
        //rf.localScale = Vector3.one;
        //rf.anchoredPosition3D = Vector3.zero;
        //rf.localEulerAngles = Vector3.zero;
        //item.ParentListView = this;
        //return item;
        return null;
    }

    GameObject GetNewPageByIndex(int index)
    {
        //GameObject page = mGetPageItemByIndex(index);
        //if(page == null)
        //{
        //    //page = Instantiate<GameObject>();
        //}
        //return page;

        return bookPages[index];
    }
    void Start()
    {
        foreground = GameObject.Instantiate<GameObject>(PageStartItem.gameObject , transform);
        background = GameObject.Instantiate<GameObject>(PageEndItem.gameObject , transform);
        foreground.transform.SetParent(PageCache.transform);
        background.transform.SetParent(PageCache.transform);
        if(bookPagesPrefabs.Length > 0)
        {
            bookPages = new GameObject[bookPagesPrefabs.Length];
            for(int i = 0; i < bookPagesPrefabs.Length; i++)
            {
                GameObject pageItem = Instantiate(bookPagesPrefabs[i]);
                pageItem.transform.SetParent(PageCache.transform);
                pageItem.transform.Reset();
                bookPages[i] = pageItem;
            }
        }
        
        float scaleFactor = 1;
        if (canvas) scaleFactor = canvas.scaleFactor;
        float pageWidth = (BookPanel.rect.width* scaleFactor - 1) / 2;
        float pageHeight = BookPanel.rect.height* scaleFactor;
        Left.gameObject.SetActive(false);
        Right.gameObject.SetActive(false);
        UpdateSprites();
        Vector3 globalsb = BookPanel.transform.position + new Vector3(0, -pageHeight / 2);
        sb = transformPoint(globalsb);
        Vector3 globalebr = BookPanel.transform.position + new Vector3(pageWidth, -pageHeight / 2);
        ebr = transformPoint(globalebr);
        Vector3 globalebl = BookPanel.transform.position + new Vector3(-pageWidth, -pageHeight / 2);
        ebl = transformPoint(globalebl);
        Vector3 globalst = BookPanel.transform.position + new Vector3(0, pageHeight / 2);
        st = transformPoint(globalst);
        radius1 = Vector2.Distance(sb, ebr);
        float scaledPageWidth = pageWidth / scaleFactor;
        float scaledPageHeight = pageHeight / scaleFactor;
        radius2 = Mathf.Sqrt(scaledPageWidth * scaledPageWidth + scaledPageHeight * scaledPageHeight);
        ClippingPlane.rectTransform.sizeDelta = new Vector2(scaledPageWidth*2, scaledPageHeight + scaledPageWidth * 2);
        Shadow.rectTransform.sizeDelta = new Vector2(scaledPageWidth, scaledPageHeight + scaledPageWidth * 0.6f);
        ShadowLTR.rectTransform.sizeDelta = new Vector2(scaledPageWidth, scaledPageHeight + scaledPageWidth * 0.6f);
        NextPageClip.rectTransform.sizeDelta = new Vector2(scaledPageWidth, scaledPageHeight + scaledPageWidth * 0.6f);
    }
    void MoveOldPageToCache(Transform t)
    {
        if(!moveExpirePageToCache )
        {
            return;
        }
        if(t!=null)
        {
            int childCnt = t.childCount;
            for(int i = childCnt - 1; i >= 0; i--)
            {
                Transform child = t.GetChild(i);
                child.SetParent(PageCache.transform);
                child.transform.Reset();
            }
        }
    }

    
    public Vector3 transformPoint(Vector3 global)
    {
        Vector2 localPos = BookPanel.InverseTransformPoint(global);
        //RectTransformUtility.ScreenPointToLocalPointInRectangle(BookPanel, global, null, out localPos);
        return localPos;
    }
    void Update()
    {
        if (pageDragging&&interactable)
        {
            UpdateBook();
        }
        //Debug.Log("mouse local pos:" + transformPoint(Input.mousePosition));
        //Debug.Log("mouse  pos:" + Input.mousePosition);
    }
    public void UpdateBook()
    {
        f= Vector3.Lerp(f,transformPoint( Input.mousePosition), Time.deltaTime * 10);
        if (mode == FlipMode.RightToLeft)
            UpdateBookRTLToPoint(f);
        else
            UpdateBookLTRToPoint(f);
    }
    public void UpdateBookLTRToPoint(Vector3 followLocation)
    {
        mode = FlipMode.LeftToRight;
        f = followLocation;
        ShadowLTR.transform.SetParent(ClippingPlane.transform, true);
        ShadowLTR.transform.localPosition = Vector3.zero;
        ShadowLTR.transform.localEulerAngles = Vector3.zero;
        Left.transform.SetParent(ClippingPlane.transform, true);

        Right.transform.SetParent(BookPanel.transform, true);
        LeftNext.transform.SetParent(BookPanel.transform, true);

        c = Calc_C_Position(followLocation);
        Vector3 t1;
        float T0_T1_Angle = Calc_T0_T1_Angle(c,ebl,out t1);
        if (T0_T1_Angle < 0) T0_T1_Angle += 180;

        ClippingPlane.transform.eulerAngles = new Vector3(0, 0, T0_T1_Angle - 90);
        ClippingPlane.transform.position = BookPanel.TransformPoint(t1);

        //page position and angle
        Left.transform.position =BookPanel.TransformPoint( c);
        float C_T1_dy = t1.y - c.y;
        float C_T1_dx = t1.x - c.x;
        float C_T1_Angle = Mathf.Atan2(C_T1_dy, C_T1_dx) * Mathf.Rad2Deg;
        Left.transform.eulerAngles = new Vector3(0, 0, C_T1_Angle - 180);

        NextPageClip.transform.eulerAngles = new Vector3(0, 0, T0_T1_Angle - 90);
        NextPageClip.transform.position = BookPanel.TransformPoint(t1);
        LeftNext.transform.SetParent(NextPageClip.transform, true);
        Right.transform.SetParent(ClippingPlane.transform, true);
        Right.transform.SetAsFirstSibling();

        ShadowLTR.rectTransform.SetParent(Left.rectTransform, true);
    }
    public void UpdateBookRTLToPoint(Vector3 followLocation)
    {
        mode = FlipMode.RightToLeft;
        f = followLocation;
        Shadow.transform.SetParent(ClippingPlane.transform, true);
        Shadow.transform.localPosition = Vector3.zero;
        Shadow.transform.localEulerAngles = Vector3.zero;
        Right.transform.SetParent(ClippingPlane.transform, true);
        
        Left.transform.SetParent(BookPanel.transform, true);
        RightNext.transform.SetParent(BookPanel.transform, true);
        c = Calc_C_Position(followLocation);
        Vector3 t1;
        float T0_T1_Angle = Calc_T0_T1_Angle(c,ebr,out t1);
        if (T0_T1_Angle >= -90) T0_T1_Angle -= 180;

        ClippingPlane.rectTransform.pivot = new Vector2(1, 0.35f);
        ClippingPlane.transform.eulerAngles = new Vector3(0, 0, T0_T1_Angle + 90);
        ClippingPlane.transform.position = BookPanel.TransformPoint(t1);

        //page position and angle
        Right.transform.position = BookPanel.TransformPoint(c);
        float C_T1_dy = t1.y - c.y;
        float C_T1_dx = t1.x - c.x;
        float C_T1_Angle = Mathf.Atan2(C_T1_dy, C_T1_dx) * Mathf.Rad2Deg;
        Right.transform.eulerAngles = new Vector3(0, 0, C_T1_Angle);

        NextPageClip.transform.eulerAngles = new Vector3(0, 0, T0_T1_Angle + 90);
        NextPageClip.transform.position = BookPanel.TransformPoint(t1);
        RightNext.transform.SetParent(NextPageClip.transform, true);
        Left.transform.SetParent(ClippingPlane.transform, true);
        Left.transform.SetAsFirstSibling();

        Shadow.rectTransform.SetParent(Right.rectTransform, true);
    }
    private float Calc_T0_T1_Angle(Vector3 c,Vector3 bookCorner,out  Vector3 t1)
    {
        Vector3 t0 = (c + bookCorner) / 2;
        float T0_CORNER_dy = bookCorner.y - t0.y;
        float T0_CORNER_dx = bookCorner.x - t0.x;
        float T0_CORNER_Angle = Mathf.Atan2(T0_CORNER_dy, T0_CORNER_dx);
        float T0_T1_Angle = 90 - T0_CORNER_Angle;
        
        float T1_X = t0.x - T0_CORNER_dy * Mathf.Tan(T0_CORNER_Angle);
        T1_X = normalizeT1X(T1_X, bookCorner, sb);
        t1 = new Vector3(T1_X, sb.y, 0);
        ////////////////////////////////////////////////
        //clipping plane angle=T0_T1_Angle
        float T0_T1_dy = t1.y - t0.y;
        float T0_T1_dx = t1.x - t0.x;
        T0_T1_Angle = Mathf.Atan2(T0_T1_dy, T0_T1_dx) * Mathf.Rad2Deg;
        return T0_T1_Angle;
    }
    private float normalizeT1X(float t1,Vector3 corner,Vector3 sb)
    {
        if (t1 > sb.x && sb.x > corner.x)
            return sb.x;
        if (t1 < sb.x && sb.x < corner.x)
            return sb.x;
        return t1;
    }
    private Vector3 Calc_C_Position(Vector3 followLocation)
    {
        Vector3 c;
        f = followLocation;
        float F_SB_dy = f.y - sb.y;
        float F_SB_dx = f.x - sb.x;
        float F_SB_Angle = Mathf.Atan2(F_SB_dy, F_SB_dx);
        Vector3 r1 = new Vector3(radius1 * Mathf.Cos(F_SB_Angle),radius1 * Mathf.Sin(F_SB_Angle), 0) + sb;

        float F_SB_distance = Vector2.Distance(f, sb);
        if (F_SB_distance < radius1)
            c = f;
        else
            c = r1;
        float F_ST_dy = c.y - st.y;
        float F_ST_dx = c.x - st.x;
        float F_ST_Angle = Mathf.Atan2(F_ST_dy, F_ST_dx);
        Vector3 r2 = new Vector3(radius2 * Mathf.Cos(F_ST_Angle),
           radius2 * Mathf.Sin(F_ST_Angle), 0) + st;
        float C_ST_distance = Vector2.Distance(c, st);
        if (C_ST_distance > radius2)
            c = r2;
        return c;
    }
 
    public void DragRightPageToPoint(Vector3 point)
    {
        if (currentPage >= bookPages.Length) return;
        pageDragging = true;
        mode = FlipMode.RightToLeft;
        f = point;


        NextPageClip.rectTransform.pivot = new Vector2(0, 0.12f);
        ClippingPlane.rectTransform.pivot = new Vector2(1, 0.35f);

        Left.gameObject.SetActive(true);
        Left.rectTransform.pivot = Vector2.zero;
        Left.transform.position = RightNext.transform.position;
        Left.transform.eulerAngles = Vector3.zero;
        //Left.sprite = (currentPage < bookPages.Length) ? bookPages[currentPage] : background;
        if(currentPage < bookPages.Length)
        {
            MoveOldPageToCache(Left.transform);
            bookPages[currentPage].gameObject.transform.SetParent(Left.transform);
            bookPages[currentPage].gameObject.transform.Reset();
        }
        else
        {
            MoveOldPageToCache(Left.transform);
            foreground.transform.SetParent(Left.transform);
            foreground.transform.Reset();
        }
        Left.transform.SetAsFirstSibling();
        
        Right.gameObject.SetActive(true);
        Right.transform.position = RightNext.transform.position;
        Right.transform.eulerAngles = Vector3.zero;
        //Right.sprite = (currentPage < bookPages.Length - 1) ? bookPages[currentPage + 1] : background;
        if(currentPage < bookPages.Length - 1)
        {
            MoveOldPageToCache(Right.transform);
            bookPages[currentPage + 1].gameObject.transform.SetParent(Right.transform);
            bookPages[currentPage + 1].gameObject.transform.Reset();
        }
        else
        {
            MoveOldPageToCache(Right.transform);
            background.transform.SetParent(Right.transform);
            background.transform.Reset();
        }
        //RightNext.sprite = (currentPage < bookPages.Length - 2) ? bookPages[currentPage + 2] : background;
        if(currentPage < bookPages.Length - 2)
        {
            MoveOldPageToCache(RightNext.transform);
            bookPages[currentPage + 2].gameObject.transform.SetParent(RightNext.transform);
            bookPages[currentPage + 2].gameObject.transform.Reset();
        }
        else
        {
            MoveOldPageToCache(RightNext.transform);
            background.transform.SetParent(RightNext.transform);
            background.transform.Reset();
        }
        LeftNext.transform.SetAsFirstSibling();
        if (enableShadowEffect) Shadow.gameObject.SetActive(true);
        UpdateBookRTLToPoint(f);
    }
    public void OnMouseDragRightPage()
    {
        if(interactable)
        {
            OnTouchStart();
            DragRightPageToPoint(transformPoint(Input.mousePosition));
        }
    }
    public void DragLeftPageToPoint(Vector3 point)
    {
        if (currentPage <= 0) return;
        pageDragging = true;
        mode = FlipMode.LeftToRight;
        f = point;

        NextPageClip.rectTransform.pivot = new Vector2(1, 0.12f);
        ClippingPlane.rectTransform.pivot = new Vector2(0, 0.35f);

        Right.gameObject.SetActive(true);
        Right.transform.position = LeftNext.transform.position;
        //Right.sprite = bookPages[currentPage - 1];
        bookPages[currentPage - 1].transform.SetParent(Right.transform);
        bookPages[currentPage - 1].transform.Reset();
        Right.transform.eulerAngles = Vector3.zero;
        Right.transform.SetAsFirstSibling();

        Left.gameObject.SetActive(true);
        Left.rectTransform.pivot = new Vector2(1, 0);
        Left.transform.position = LeftNext.transform.position;
        Left.transform.eulerAngles = Vector3.zero;
        //Left.sprite = (currentPage >= 2) ? bookPages[currentPage - 2] : background;
        if(currentPage >= 2)
        {
            MoveOldPageToCache(Left.transform);
            bookPages[currentPage - 2].gameObject.transform.SetParent(Left.transform);
            bookPages[currentPage - 2].gameObject.transform.Reset();
        }
        else
        {
            MoveOldPageToCache(Left.transform);
            foreground.transform.SetParent(Left.transform);
            foreground.transform.Reset();
        }
        //LeftNext.sprite = (currentPage >= 3) ? bookPages[currentPage - 3] : background;
        if(currentPage >= 3)
        {
            MoveOldPageToCache(LeftNext.transform);
            bookPages[currentPage - 3].gameObject.transform.SetParent(LeftNext.transform);
            bookPages[currentPage - 3].gameObject.transform.Reset();
        }
        else
        {
            MoveOldPageToCache(LeftNext.transform);
            foreground.transform.SetParent(LeftNext.transform);
            foreground.transform.Reset();
        }
        RightNext.transform.SetAsFirstSibling();
        if (enableShadowEffect) ShadowLTR.gameObject.SetActive(true);
        UpdateBookLTRToPoint(f);
    }
    public void OnMouseDragLeftPage()
    {
        if(interactable)
        {
            OnTouchStart();
            DragLeftPageToPoint(transformPoint(Input.mousePosition));
        }
    }
    void OnTouchStart()
    {
        touchStartPos = Input.mousePosition;
        touchStartTime = Time.time;
    }

    public void OnMouseRelease()
    {
        if(interactable)
        {
            touchEndPos = Input.mousePosition;
            ReleasePage();
        }
    }
    public void ReleasePage()
    {
        if (pageDragging)
        {
            pageDragging = false;
            float distance = Vector3.Distance(touchStartPos , touchEndPos);
            float diffTime = Time.time - touchStartTime;
            float speed = distance / diffTime;
            //Debug.Log("speed = " + speed);
            if(speed > flipForwardSpeed)
            {
                TweenForward();
            }
            else
            {
                float distanceToLeft = Vector2.Distance(c , ebl);
                float distanceToRight = Vector2.Distance(c , ebr);
                if(distanceToRight < distanceToLeft && mode == FlipMode.RightToLeft)
                    TweenBack();
                else if(distanceToRight > distanceToLeft && mode == FlipMode.LeftToRight)
                    TweenBack();
                else
                    TweenForward();
            }
        }
    }
    Coroutine currentCoroutine;
    void UpdateSprites()
    {
        //LeftNext.sprite= (currentPage > 0 && currentPage <= bookPages.Length) ? bookPages[currentPage-1] : background;
        if(currentPage > 0 && currentPage <= bookPages.Length)
        {
            MoveOldPageToCache(LeftNext.transform);
            var page = GetNewPageByIndex(currentPage - 1);
            page.gameObject.transform.SetParent(LeftNext.transform);
            page.transform.Reset();
        }
        else
        {
            MoveOldPageToCache(LeftNext.transform);
            foreground.transform.SetParent(LeftNext.transform);
            foreground.transform.Reset();
        }
        //RightNext.sprite=(currentPage>=0 &&currentPage<bookPages.Length) ? bookPages[currentPage] : background;
        if(currentPage >= 0 && currentPage < bookPages.Length)
        {
            MoveOldPageToCache(RightNext.transform);
            var page = GetNewPageByIndex(currentPage);
            page.gameObject.transform.SetParent(RightNext.transform);
            page.gameObject.transform.Reset();
        }
        else
        {
            MoveOldPageToCache(RightNext.transform);
            background.transform.SetParent(RightNext.transform);
            background.transform.Reset();
        }
    }
    public void TweenForward()
    {
        if(mode== FlipMode.RightToLeft)
        currentCoroutine = StartCoroutine(TweenTo(ebl, 0.15f, () => { Flip(); }));
        else
        currentCoroutine = StartCoroutine(TweenTo(ebr, 0.15f, () => { Flip(); }));
    }
    void Flip()
    {
        if (mode == FlipMode.RightToLeft)
            currentPage += 2;
        else
            currentPage -= 2;
        LeftNext.transform.SetParent(BookPanel.transform, true);
        Left.transform.SetParent(BookPanel.transform, true);
        LeftNext.transform.SetParent(BookPanel.transform, true);
        Left.gameObject.SetActive(false);
        Right.gameObject.SetActive(false);
        Right.transform.SetParent(BookPanel.transform, true);
        RightNext.transform.SetParent(BookPanel.transform, true);
        UpdateSprites();
        Shadow.gameObject.SetActive(false);
        ShadowLTR.gameObject.SetActive(false);
        if (OnFlip != null)
            OnFlip.Invoke();
    }
    public void TweenBack()
    {
        if (mode == FlipMode.RightToLeft)
        {
            currentCoroutine = StartCoroutine(TweenTo(ebr,0.15f,
                () =>
                {
                    UpdateSprites();
                    RightNext.transform.SetParent(BookPanel.transform);
                    Right.transform.SetParent(BookPanel.transform);

                    Left.gameObject.SetActive(false);
                    Right.gameObject.SetActive(false);
                    pageDragging = false;
                }
                ));
        }
        else
        {
            currentCoroutine = StartCoroutine(TweenTo(ebl, 0.15f,
                () =>
                {
                    UpdateSprites();

                    LeftNext.transform.SetParent(BookPanel.transform);
                    Left.transform.SetParent(BookPanel.transform);

                    Left.gameObject.SetActive(false);
                    Right.gameObject.SetActive(false);
                    pageDragging = false;
                }
                ));
        }
    }
    public IEnumerator TweenTo(Vector3 to, float duration, System.Action onFinish)
    {
        int steps = (int)(duration / 0.025f);
        Vector3 displacement = (to - f) / steps;
        for (int i = 0; i < steps-1; i++)
        {
            if(mode== FlipMode.RightToLeft)
            UpdateBookRTLToPoint( f + displacement);
            else
                UpdateBookLTRToPoint(f + displacement);

            yield return new WaitForSeconds(0.025f);
        }
        if (onFinish != null)
            onFinish();
    }
    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}
