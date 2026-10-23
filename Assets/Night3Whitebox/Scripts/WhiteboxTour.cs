using UnityEngine;

public class WhiteboxTour : MonoBehaviour
{
    public Camera walkCamera, overviewCamera;
    public GameObject ceiling, annotations;
    public Renderer[] upperWalls;
    public CharacterController body;
    public WhiteboxDoor[] doors;
    public bool overview = true;
    public bool automated;
    public float walkSpeed = 2.05f, crouchSpeed = 1.05f;
    public Vector3 spawn = new Vector3(3, .03f, 12);
    public string room = "YOUR ROOM";
    float pitch, eye = 1.37f;
    Vector3 velocity;
    bool crouching;
    GUIStyle heading, small, caption;

    void Start() { SetOverview(overview); }
    public void SetOverview(bool value)
    {
        overview = value;
        walkCamera.enabled = !value;
        overviewCamera.enabled = value;
        ceiling.SetActive(!value);
        annotations.SetActive(value);
        foreach (var r in upperWalls) if (r) r.enabled = !value;
        body.enabled = !value;
        velocity = Vector3.zero;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    public void ResetPosition()
    {
        body.enabled = false;
        transform.position = spawn;
        transform.rotation = Quaternion.Euler(0,180,0);
        pitch = 0; velocity = Vector3.zero;
        walkCamera.transform.localRotation = Quaternion.identity;
        body.enabled = !overview;
    }
    void Update()
    {
        if (automated) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetOverview(false);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetOverview(true);
        if (Input.GetKeyDown(KeyCode.R)) ResetPosition();
        if (Input.GetKeyDown(KeyCode.Escape)) ReleaseInput();
        if (overview) return;
        if (Input.GetMouseButtonDown(0)) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            transform.Rotate(0, Input.GetAxisRaw("Mouse X") * 1.9f, 0);
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 1.7f, -55, 46);
            walkCamera.transform.localRotation = Quaternion.Euler(pitch,0,0);
        }
        if (Input.GetKeyDown(KeyCode.C)) crouching = !crouching;
        float targetHeight = crouching ? 1.05f : 1.55f;
        // Do not stand through a low obstacle.
        if (!crouching && Physics.CheckCapsule(transform.position + Vector3.up*.25f,
            transform.position + Vector3.up*1.35f, .19f, ~(1 << gameObject.layer), QueryTriggerInteraction.Ignore))
            targetHeight = 1.05f;
        body.height = targetHeight; body.center = new Vector3(0, targetHeight/2,0);
        eye = Mathf.Lerp(eye, targetHeight < 1.1f ? .85f : 1.37f, 1-Mathf.Exp(-16*Time.deltaTime));
        walkCamera.transform.localPosition = Vector3.up*eye;
        Vector3 direction = Vector3.zero;
        if (Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)) direction += transform.forward;
        if (Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)) direction -= transform.forward;
        if (Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)) direction -= transform.right;
        if (Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)) direction += transform.right;
        float speed = crouching ? crouchSpeed : walkSpeed;
        velocity = Vector3.Lerp(velocity, direction.normalized * speed,
            1-Mathf.Exp(-(direction.sqrMagnitude>0 ? 24 : 36)*Time.deltaTime));
        body.Move((velocity + Vector3.down*3)*Mathf.Min(Time.deltaTime,.06f));
        if (Input.GetKeyDown(KeyCode.E))
        {
            WhiteboxDoor nearest=null; float best=1.7f;
            foreach (var door in doors)
            {
                float d=Vector2.Distance(new Vector2(transform.position.x,transform.position.z),
                    new Vector2(door.transform.position.x+.47f,door.transform.position.z));
                if (d<best) { best=d; nearest=door; }
            }
            if(nearest) nearest.Toggle();
        }
    }
    void ReleaseInput() { velocity=Vector3.zero; Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
    void OnApplicationFocus(bool hasFocus) { if(!hasFocus) ReleaseInput(); }
    void OnDisable() { ReleaseInput(); }
    void OnGUI()
    {
        if(heading==null)
        {
            heading=new GUIStyle(GUI.skin.label){fontSize=23,fontStyle=FontStyle.Bold}; heading.normal.textColor=new Color(.94f,.96f,.94f);
            small=new GUIStyle(GUI.skin.label){fontSize=13}; small.normal.textColor=new Color(.76f,.84f,.83f);
            caption=new GUIStyle(small){fontSize=12};
        }
        GUI.color=new Color(.055f,.085f,.11f,.94f); GUI.DrawTexture(new Rect(0,0,Screen.width,86),Texture2D.whiteTexture); GUI.color=Color.white;
        GUI.Label(new Rect(26,12,600,34),"GET YOUR NIGHT BACK",heading);
        GUI.Label(new Rect(27,48,700,24),"NIGHT 03   /   FOOTSTEPS CLOSER     ·     MODULAR WHITEBOX",small);
        string mode = overview ? "02 / SPATIAL OVERVIEW" : "01 / WALK THE SPACE";
        GUI.Label(new Rect(Screen.width-240,29,230,27),mode,small);
        GUI.color=new Color(.055f,.085f,.11f,.94f);GUI.DrawTexture(new Rect(0,Screen.height-58,Screen.width,58),Texture2D.whiteTexture);GUI.color=Color.white;
        GUI.Label(new Rect(26,Screen.height-49,Screen.width-45,24),"1  Walk    2  Overview    WASD  Move    Click  Look    C  Crouch    E  Door    R  Reset    Esc  Release mouse",small);
        GUI.Label(new Rect(26,Screen.height-26,Screen.width-45,22),overview ? "Start in your room. Explore both routes. Reach the laundry drawer, then return. Furniture and walls define the choices." : "Scale study: 2.05 units/s walk · 1.05 units/s crouch · 0.40 unit body width. Doors are simplified for spatial testing.",caption);
        if(!overview)
        {
            GUI.color=new Color(.95f,.95f,.9f,.9f);GUI.DrawTexture(new Rect(Screen.width/2-2,Screen.height/2-2,4,4),Texture2D.whiteTexture);GUI.color=Color.white;
        }
    }
}
