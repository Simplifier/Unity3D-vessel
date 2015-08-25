using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Main : MonoBehaviour {
	public int shipStartY = -200;
	public int startPieceH = 100;
	public int startWidth = 200;
	public int minWidth = 50;
	public int startSpeed = 500;
	public float speedDelta = .5f;

	private Game defaultGame;
	private readonly Inputs input = new Inputs(0, .034f, false);
	//private Sprite container = new Sprite();
	private Material material;
	private const float pixelsPerUnit = 50;

	void Start () {
		defaultGame = getDefaultGame();
		createMaterial ();
	}

	private void createMaterial(){
		// Unity has a built-in shader that is useful for drawing
		// simple colored things.
		var shader = Shader.Find ("Hidden/Internal-Colored");
		material = new Material (shader);
		material.hideFlags = HideFlags.HideAndDontSave;
		// Turn on alpha blending
		material.SetInt ("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		material.SetInt ("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		// Turn backface culling off
		material.SetInt ("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
		// Turn off depth writes
		material.SetInt ("_ZWrite", 0);
		material.SetPass (0);
	}

	void Update() {
	    var left = Input.GetKey(KeyCode.LeftArrow);
        var right = Input.GetKey(KeyCode.RightArrow);
	    input.dir = 0;
        if(left && !right){
            input.dir = -1;
        } else if (!left && right) {
            input.dir = 1;
        }
        input.space = Input.GetKey(KeyCode.Space);
	    input.delta = Time.deltaTime;

		defaultGame = stepStart(input, defaultGame);
		display(defaultGame);
	}

	private Game getDefaultGame() {
		var pieces = new List<Piece>();
		for (var n = -60; n <= 0; n++) {
			pieces.Add(new Piece(curve(n, 20), (n + 30) * 10, 0, -startSpeed, startWidth, startPieceH));
		}

		return new Game{
		    cnt = 0,
		    score = 0,
		    ship = new Ship(0, shipStartY, 0, 0),
		    t = new Tunnel(startWidth, startSpeed, curve(0, 20), 300, startPieceH, 20),
		    debri = new List<Debri>(),
		    state = State.Waiting,
			pieces = pieces
		};
	}

	private float degrees(float d) {
		return d * Mathf.PI / 180;
	}

	//updates
	private float curve(int cnt, float ampl) {
		var degree = degrees(cnt) * 2;
		var segment = (int)Mathf.Floor((float)cnt / 200) % 7; //one more than # of segs
		switch (segment) {
			case 0:
				return Mathf.Sin(degree * 4) * ampl;
			case 1:
				return (Mathf.Cos(degree * 6) + Mathf.Sin(2 * degree)) * ampl;
			case 2:
				return (Mathf.Cos(degree * 3) + Mathf.Sin(2 * degree)) * ampl;
			case 3:
				return 200;
			case 4:
				return (Mathf.Cos(degree * 3) + Mathf.Sin(2 * degree)) * ampl;
			case 5:
				return (Mathf.Cos(degree) + Mathf.Cos(degree * 3)) * ampl;
			case 6:
				return 0;
		}
		return 0;
	}

	private float towards(float target, float x) {
		var xdelta = target - x;
		return x + (xdelta / 30);
	}

	private float updateAmpl(float cur, float max) {
		if (cur < max)
			return cur + 1;
		return cur;
	}

	private bool withinN(float offset, float px, float sx) {
		return (sx > px - offset) && (sx < px + offset);
	}

	private Tunnel updateTunnel(Game game) {
		var t = game.t;
		var state = game.state;
		var speed = game.state == State.Playing ? t.speed + speedDelta : t.speed;
		var ampl = game.state == State.Playing ? updateAmpl(t.ampl, 180) : t.ampl;
		var next = curve(game.cnt, ampl);
		var nx = withinN(2, next, t.x) ? next : towards(next, t.x);
		var nwidth = t.width < minWidth || state == State.Waiting ? t.width : t.width - .1f;

		t.x = nx;
		t.width = nwidth;
		t.speed = speed;
		t.ampl = ampl;
		t.h += speedDelta;

		return t;
	}

	private List<Piece> addPiece(Game game) {
		var t = game.t;
		var h = (800 / game.pieces.Count) + 50;
		game.pieces.Add(new Piece(t.x, t.y, 0, -t.speed, t.width, h));

		return game.pieces;
	}

	private  List<Piece> stepPieces(float t, Game game) {
		return addPiece(game)
            .Select(piece => piece.stepObj(t) as Piece)
            .Where(piece => piece.filter()).ToList();
	}

	private Debri addD(float sx, float sy, int n) {
		var vx = Mathf.Sin(degrees(n)) * 150;
		var vy = Mathf.Cos(degrees(n)) * 150;
		return new Debri(sx, sy, vx, vy, 10);
	}

	private List<Debri> addDebri(Ship ship, int cnt, List<Debri> debri) {
		var l = debri.Count();
		if (l == 0) {
			for (var n = 0; n <= 360; n++) {
				if (n % 12 == 0) {
					debri.Insert(0, addD(ship.x, ship.y, n));
				}
			}
		} else {
			debri.Select(debris => {
				Debug.Log(11);
			    debris.deg = (debris.deg + 20) % 360;
                return debris;
			});
		}
		return debri;
	}

	private List<Debri> stepDebri(float t, int cnt, Ship ship, List<Debri> debri){
	    return addDebri(ship, cnt, debri).Select(debris => debris.stepObj(t) as Debri).ToList();
	}

	private bool inside(Ship ship, Piece piece) {
		return withinN(piece.width / 2, piece.x, ship.x);
	}

	private State updateState(Game game) {
	    var pieces = game.pieces.Where(p => withinN(60, p.y, game.ship.y));
		return pieces.Any(piece => inside(game.ship, piece)) ? State.Playing : State.Dead;
	}

	private Ship hideShip(Ship s) {
		s.x = -30;
		s.y = 400;
		return s;
	}

	private Ship autoShip(Tunnel t, Ship s) {
		s.x = towards(t.x, s.x);
		return s;
	}

	private Game stepDead(Inputs input, Game game){
		if (input.space) {
			defaultGame = getDefaultGame();
			defaultGame.state = State.Playing;
			return defaultGame;
		}
		if (game.cnt > game.score + 200) {
		    defaultGame = getDefaultGame();
		    defaultGame.state = State.Waiting;
		    return defaultGame;
		}
		game.debri = stepDebri(input.delta, game.cnt, game.ship, game.debri);
		game.ship = hideShip(game.ship);
		game.cnt++;
		return game;
	}

	private Game stepWaiting(Inputs input, Game game) {
		if (input.space) {
			defaultGame = getDefaultGame();
			defaultGame.state = State.Playing;
			return defaultGame;
		} else {
			game.pieces = stepPieces(input.delta, game);
			game.ship = autoShip(game.t, game.ship);
			game.t = updateTunnel(game);
			game.cnt++;
			return game;
		}
	}

	private Game stepGame(Inputs input, Game game) {
		game.pieces = stepPieces(input.delta, game);
		game.cnt++;
		game.t = updateTunnel(game);
		game.ship.step(input.delta, input.dir);
		game.score++;
		game.state = updateState(game);
		return game;
	}

	private Game stepStart(Inputs input, Game game) {
		switch(game.state) {
			case State.Playing:
				return stepGame(input, game);
			case State.Dead:
				return stepDead(input, game);
			case State.Waiting:
				return stepWaiting(input, game);
		}
		return null;
	}
	
	//DISPLAY
	/*private void drawPiece(Piece piece){
		var p:Shape = new Shape;
		p.graphics.beginFill(0xef2929);
		p.graphics.drawRect(-piece.width / 2, -piece.height / 2, piece.width, piece.height);
		p.x = piece.x;
		p.y = -piece.y;
		return p;
	}*/

	private void drawDebri(Debri d) {
		Mesh mesh = new Mesh();
		mesh.vertices = new Vector3[]{
			new Vector3(0, 0, 0), 
			new Vector3(10 / pixelsPerUnit, 0, 0),
			new Vector3(5 / pixelsPerUnit, 8.66f / pixelsPerUnit, 0)
		};
		mesh.triangles = new int[] {0, 1, 2};
		var rotation = Quaternion.Euler(0, 0, d.deg);
		Graphics.DrawMesh (mesh, new Vector3(d.x / pixelsPerUnit, d.y / pixelsPerUnit, 0), rotation, material, 0);
	}
	
	/*private Sprite txt(string t) {
		var tf = new TextField;
		tf.autoSize = TextFieldAutoSize.LEFT;
		
		var format:TextFormat = new TextFormat();
		format.font = "Arial";
		format.color = 0xffffff;
		format.size = 15;
		
		tf.defaultTextFormat = format;
		tf.text = t;
		tf.x = -int(tf.width / 2);
		tf.y = -int(tf.height / 2);
		
		var c:Sprite = new Sprite;
		c.addChild(tf);
		return c;
	}
	
	private string displayText(Game game) {
		switch(game.state) {
		case State.Playing:
			return "";
		case State.Dead:
			return game.score.ToString();
		case State.Waiting:
			return "Space to start then arrows";
		default:
			throw new Error("invalid state");
		}
	}*/
	
	private void drawShip(Ship ship) {
		Mesh mesh = new Mesh();
		mesh.vertices = new Vector3[]{
			new Vector3(0, 0, 0), 
			new Vector3(20 / pixelsPerUnit, 0, 0),
			new Vector3(10 / pixelsPerUnit, 17 / pixelsPerUnit, 0)
		};
		mesh.triangles = new int[] {0, 1, 2};
		Graphics.DrawMesh (mesh, new Vector3(ship.x / pixelsPerUnit, ship.y / pixelsPerUnit, 0), Quaternion.identity, material, 0);
	}
	
	private void display(Game game) {
		foreach(var piece in game.pieces) {
			//drawPiece(piece);
		}
		drawShip(game.ship);
		foreach(var debri in game.debri) {
			drawDebri(debri);
		}
		//displayText(game);
	}
}

class Inputs {
	public int dir;
	public float delta;
	public bool space;
	public  Inputs(int dir, float delta, bool space) {
		this.dir = dir;
		this.delta = delta;
		this.space = space;
	}
}

class Tunnel {
	public float ampl;
	public float h;
	public float y;
	public float x;
	public float speed;
	public float width;

	public  Tunnel(float width, float speed, float x, float y, float h, float ampl) {
		this.ampl = ampl;
		this.h = h;
		this.y = y;
		this.x = x;
		this.speed = speed;
		this.width = width;
	}
}

class GameObject {
	public float vy;
	public float vx;
	public float y;
	public float x;

	public GameObject stepObj(float t) {
		x += vx * t;
		y += vy * t;
		return this;
	}
}

class Ship : GameObject {
	public  Ship(float x, float y, float vx, float vy) {
		this.vy = vy;
		this.vx = vx;
		this.y = y;
		this.x = x;
	}

	public Ship step(float t, int dir) {
		vx = dir * 360;
		return stepObj(t) as Ship;
	}
}

class Piece : GameObject {
	public float height;
	public float width;

	public  Piece(float x, float y, float vx, float vy, float width, float height) {
		this.height = height;
		this.width = width;
		this.vy = vy;
		this.vx = vx;
		this.y = y;
		this.x = x;
	}

	public bool filter() {
		return y > -400;
	}
}

class Debri : GameObject {
	public int deg;

	public  Debri(float x, float y, float vx, float vy, int deg) {
		this.deg = deg;
		this.vy = vy;
		this.vx = vx;
		this.y = y;
		this.x = x;
	}
}

class Game {
	public int cnt;
	public  List<Debri> debri;
	public Ship ship;
	public Tunnel t;
	public List<Piece> pieces;
	public int score;
	public State state;
}

enum State {
    Waiting,
    Playing,
    Dead
};