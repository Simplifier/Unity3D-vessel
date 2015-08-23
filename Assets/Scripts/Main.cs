package {
	import flash.display.Shape;
	import flash.display.Sprite;
	import flash.events.Event;
	import flash.events.KeyboardEvent;
	import flash.text.TextField;
	import flash.text.TextFieldAutoSize;
	import flash.text.TextFormat;
	import flash.ui.Keyboard;

	/**
	 * ...
	 * @author Alex
	 */
	public class Main extends Sprite {
		public static const shipStartY:int = -200;
		public static const startPieceH:int = 100;
		public static const startWidth:int = 200;
		public static const minWidth:int = 50;
		public static const startSpeed:int = 500;
		public static const speedDelta:int = 0.5;

		private var defaultGame:Game;
		private var input:Input = new Input(0, .034, false);
		private var container:Sprite = new Sprite;

		public function Main():void {
			init();
		}

		private function init():void {
			defaultGame = getDefaultGame();

			graphics.beginFill(0xa40000);
			graphics.drawRect(0, 0, stage.stageWidth, stage.stageHeight);
			graphics.endFill();

			container.x = int(stage.stageWidth / 2);
			container.y = int(stage.stageHeight / 2);
			addChild(container);

			addEventListener(Event.ENTER_FRAME, enterFrameHandler);
			stage.addEventListener(KeyboardEvent.KEY_DOWN, stage_keyDownHandler);
			stage.addEventListener(KeyboardEvent.KEY_UP, stage_keyUpHandler);
		}

		private function getDefaultGame():Game {
			var game:Game = new Game;
			game.cnt = 0;
			game.score = 0;
			game.ship = new Ship(0, shipStartY, 0, 0);
			game.t = new Tunnel(startWidth, startSpeed, curve(0, 20), 300, startPieceH, 20);
			game.debri = new Vector.<Debri>;
			game.state = State.Waiting;

			var pieces:Vector.<Piece> = new Vector.<Piece>;
			for (var n:int = -60; n <= 0; n++) {
				pieces.push(new Piece(curve(n, 20), (n + 30) * 10, 0, -startSpeed, startWidth, startPieceH));
			}
			game.pieces = pieces;
			return game;
		}

		private function enterFrameHandler(e:Event):void {
			defaultGame = stepStart(input, defaultGame);
			display(defaultGame);
		}

		private function stage_keyDownHandler(e:KeyboardEvent):void {
			switch(e.keyCode) {
				case Keyboard.LEFT:
					input.dir = -1;
					break;
				case Keyboard.RIGHT:
					input.dir = 1;
					break;
				case Keyboard.SPACE:
					input.space = true;
					break;
			}
		}

		private function stage_keyUpHandler(e:KeyboardEvent):void {
			switch(e.keyCode) {
				case Keyboard.LEFT:
				case Keyboard.RIGHT:
					input.dir = 0;
					break;
				case Keyboard.SPACE:
					input.space = false;
					break;
			}
		}

		private function degrees(d:Number):Number {
			return d * Math.PI / 180;
		}

		//updates
		private function curve(cnt:int, ampl:Number):Number {
			var degree:Number = degrees(cnt) * 2;
			var segment:int = Math.floor(cnt / 200) % 7; //one more than # of segs
			switch (segment) {
				case 0:
					return Math.sin(degree * 4) * ampl;
				case 1:
					return (Math.cos(degree * 6) + Math.sin(2 * degree)) * ampl;
				case 2:
					return (Math.cos(degree * 3) + Math.sin(2 * degree)) * ampl;
				case 3:
					return 200;
				case 4:
					return (Math.cos(degree * 3) + Math.sin(2 * degree)) * ampl;
				case 5:
					return (Math.cos(degree) + Math.cos(degree * 3)) * ampl;
				case 6:
					return 0;
			}
			return 0;
		}

		private function towards(target:Number, x:Number):Number {
			var xdelta:Number = target - x;
			return x + (xdelta / 30);
		}

		private function updateAmpl(cur:Number, max:Number):Number {
			if (cur < max)
				return cur + 1;
			return cur;
		}

		private function withinN(offset:Number, px:Number, sx:Number):Boolean {
			return (sx > px - offset) && (sx < px + offset);
		}

		private function updateTunnel(game:Game):Tunnel {
			var t:Tunnel = game.t;
			var state:String = game.state;
			var speed:Number = game.state == State.Playing ? t.speed + speedDelta : t.speed;
			var ampl:Number = game.state == State.Playing ? updateAmpl(t.ampl, 180) : t.ampl;
			var next:Number = curve(game.cnt, ampl);
			var nx:Number = withinN(2, next, t.x) ? next : towards(next, t.x);
			var nwidth:Number = t.width < minWidth || state == State.Waiting ? t.width : t.width - .1;

			t.x = nx;
			t.width = nwidth;
			t.speed = speed;
			t.ampl = ampl;
			t.h += speedDelta;

			return t;
		}

		private function addPiece(game:Game):Vector.<Piece> {
			var t:Tunnel = game.t;
			var h:Number = (800 / game.pieces.length) + 50;
			game.pieces.push(new Piece(t.x, t.y, 0, -t.speed, t.width, h));

			return game.pieces;
		}

		private function stepPieces(t:Number, game:Game):Vector.<Piece> {
			return addPiece(game).map(function(piece:Piece, index:int, vector:Vector.<Piece>):Piece {
				return piece.stepObj(t) as Piece;
			}).filter(function(piece:Piece, index:int, vector:Vector.<Piece>):Boolean {
				return piece.filter();
			});
		}

		private function addD(sx:Number, sy:Number, n:int):Debri {
			var vx:Number = Math.sin(degrees(n)) * 150;
			var vy:Number = Math.cos(degrees(n)) * 150;
			return new Debri(sx, sy, vx, vy, 10);
		}

		private function addDebri(ship:Ship, cnt:int, debri:Vector.<Debri>):Vector.<Debri> {
			var l:int = debri.length;
			var d:int = cnt % 360;
			if (l == 0) {
				for (var n:int = 0; n <= 360; n++) {
					if (n % 12 == 0) {
						debri.unshift(addD(ship.x, ship.y, n));
					}
				}
			} else {
				debri.map(function(debris:Debri, index:int, vector:Vector.<Debri>):Debri {
					debris.deg = (debris.deg + 20) % 360;
					return debris;
				});
			}
			return debri;
		}

		private function stepDebri(t:Number, cnt:int, ship:Ship, debri:Vector.<Debri>):Vector.<Debri> {
			return addDebri(ship, cnt, debri).map(function(debri:Debri, index:int, vector:Vector.<Debri>):Debri {
				return debri.stepObj(t) as Debri;
			});
		}

		private function inside(ship:Ship, piece:Piece):Boolean {
			return withinN(piece.width / 2, piece.x, ship.x);
		}

		private function updateState(game:Game):String {
			var pieces:Vector.<Piece> = game.pieces.filter(function(p:Piece, index:int, vector:Vector.<Piece>):Boolean {
				return withinN(60, p.y, game.ship.y);
			});
			for each(var piece:Piece in pieces) {
				if (inside(game.ship, piece)) return State.Playing;
			}
			return State.Dead;
		}

		private function hideShip(s:Ship):Ship {
			s.x = -30;
			s.y = 400;
			return s;
		}

		private function autoShip(t:Tunnel, s:Ship):Ship {
			s.x = towards(t.x, s.x);
			return s;
		}

		private function stepDead(input:Input, game:Game):Game {
			if (input.space) {
				defaultGame = getDefaultGame();
				defaultGame.state = State.Playing;
				return defaultGame;
			} else {
				if (game.cnt > game.score + 200) {
					defaultGame = getDefaultGame();
					defaultGame.state = State.Waiting;
					return defaultGame;
				} else {
					game.debri = stepDebri(input.delta, game.cnt, game.ship, game.debri);
					game.ship = hideShip(game.ship);
					game.cnt++;
					return game;
				}
			}
		}

		private function stepWaiting(input:Input, game:Game):Game {
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

		private function stepGame(input:Input, game:Game):Game {
			game.pieces = stepPieces(input.delta, game);
			game.cnt++;
			game.t = updateTunnel(game);
			game.ship.step(input.delta, input.dir);
			game.score++;
			game.state = updateState(game);
			return game;
		}

		private function stepStart(input:Input, game:Game):Game {
			switch(game.state) {
				case State.Playing:
					return stepGame(input, game);
				case State.Dead:
					return stepDead(input, game);
				case State.Waiting:
					return stepWaiting(input, game);
				default:
					throw new Error('invalid state');
			}
		}

		//DISPLAY
		private function drawPiece(piece:Piece):Shape {
			var p:Shape = new Shape;
			p.graphics.beginFill(0xef2929);
			p.graphics.drawRect(-piece.width / 2, -piece.height / 2, piece.width, piece.height);
			p.x = piece.x;
			p.y = -piece.y;
			return p;
		}

		private function drawDebri(d:Debri):Sprite {
			var triangle:Shape = new Shape;
			triangle.graphics.beginFill(0xffffff);
			triangle.graphics.lineTo(10, 0);
			triangle.graphics.lineTo(5, 8.66);
			triangle.graphics.lineTo(0, 0);
			triangle.x = -triangle.width / 2;
			triangle.y = -triangle.height / 2;

			var c:Sprite = new Sprite;
			c.rotation = d.deg;
			c.x = d.x;
			c.y = -d.y;
			c.addChild(triangle);

			return c;
		}

		private function drawShip(ship:Ship):Sprite {
			var triangle:Shape = new Shape;
			triangle.graphics.beginFill(0xffffff);
			triangle.graphics.lineTo(20, 0);
			triangle.graphics.lineTo(10, -17);
			triangle.graphics.lineTo(0, 0);
			triangle.x = -triangle.width / 2;
			triangle.y = triangle.height / 2;

			var c:Sprite = new Sprite;
			c.x = ship.x;
			c.y = -ship.y;
			c.addChild(triangle);

			return c;
		}

		private function txt(t:String):Sprite {
			var tf:TextField = new TextField;
			tf.autoSize = TextFieldAutoSize.LEFT;

			var format:TextFormat = new TextFormat();
			format.font = 'Arial';
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

		private function displayText(game:Game):String {
			switch(game.state) {
				case State.Playing:
					return '';
				case State.Dead:
					return game.score.toString();
				case State.Waiting:
					return "Space to start then arrows";
				default:
					throw new Error('invalid state');
			}
		}

		private function display(game:Game):void {
			container.removeChildren();
			for each(var piece:Piece in game.pieces) {
				container.addChild(drawPiece(piece));
			}
			container.addChild(drawShip(game.ship));
			for each(var debri:Debri in game.debri) {
				container.addChild(drawDebri(debri));
			}
			container.addChild(txt(displayText(game)));
		}
	}
}

class Input {
	public var dir:int;
	public var delta:Number;
	public var space:Boolean;
	public function Input(dir:int, delta:Number, space:Boolean) {
		this.dir = dir;
		this.delta = delta;
		this.space = space;
	}
}

class Tunnel {
	public var ampl:Number;
	public var h:Number;
	public var y:Number;
	public var x:Number;
	public var speed:Number;
	public var width:Number;

	public function Tunnel(width:Number, speed:Number, x:Number, y:Number, h:Number, ampl:Number) {
		this.ampl = ampl;
		this.h = h;
		this.y = y;
		this.x = x;
		this.speed = speed;
		this.width = width;

	}
}

class GameObject {
	public var vy:Number;
	public var vx:Number;
	public var y:Number;
	public var x:Number;

	public function stepObj(t:Number):GameObject {
		x += vx * t;
		y += vy * t;
		return this;
	}
}

class Ship extends GameObject {
	public function Ship(x:Number, y:Number, vx:Number, vy:Number) {
		this.vy = vy;
		this.vx = vx;
		this.y = y;
		this.x = x;
	}

	public function step(t:Number, dir:int):Ship {
		vx = dir * 360;
		return stepObj(t) as Ship;
	}
}

class Piece extends GameObject {
	public var height:Number;
	public var width:Number;

	public function Piece(x:Number, y:Number, vx:Number, vy:Number, width:Number, height:Number) {
		this.height = height;
		this.width = width;
		this.vy = vy;
		this.vx = vx;
		this.y = y;
		this.x = x;
	}

	public function filter():Boolean {
		return y > -400;
	}
}

class Debri extends GameObject {
	public var deg:int;

	public function Debri(x:Number, y:Number, vx:Number, vy:Number, deg:int) {
		this.deg = deg;
		this.vy = vy;
		this.vx = vx;
		this.y = y;
		this.x = x;
	}
}

class Game {
	public var cnt:int;
	public var debri:Vector.<Debri>;
	public var ship:Ship;
	public var t:Tunnel;
	public var pieces:Vector.<Piece>;
	public var score:int;
	public var state:String;
	public function toString():String {
		var json:Object = {
			cnt:cnt,
			debri:debri,
			ship:ship,
			t:t,
			pieces:pieces,
			state:state
		};
		return JSON.stringify(json);
	}
}

class State {
	public static const Waiting:String = 'waiting';
	public static const Playing:String = 'playing';
	public static const Dead:String = 'dead';
}