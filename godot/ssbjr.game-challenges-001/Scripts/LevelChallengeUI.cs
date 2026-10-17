using Godot;
using System.Collections.Generic;

public partial class LevelChallenge
{
	private void LoadTextures()
	{
		texFlat = GD.Load<Texture2D>("res://UI/Icons/Flat.png");
		texQuadExt = GD.Load<Texture2D>("res://UI/Icons/QuadExt.png");
		texQuadInt = GD.Load<Texture2D>("res://UI/Icons/QuadInt.png");
		texCircExt = GD.Load<Texture2D>("res://UI/Icons/CircExt.png");
		texCircInt = GD.Load<Texture2D>("res://UI/Icons/CircInt.png");
		texCruzExt = GD.Load<Texture2D>("res://UI/Icons/CruzExt.png");
		texCruzInt = GD.Load<Texture2D>("res://UI/Icons/CruzInt.png");
	}

	private void CreateTargetUI()
	{
		CanvasLayer canvas = new CanvasLayer();
		AddChild(canvas);

		uiContainer = new Control();
		uiContainer.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		uiContainer.Position = new Vector2(-420, 20); 
		canvas.AddChild(uiContainer);

		// O Fundo da Planta recorta as imagens fora da margem
		mapBackground = new ColorRect();
		mapBackground.Color = new Color(0.02f, 0.05f, 0.1f, 0.95f); 
		mapBackground.Size = new Vector2(400, 320);
		mapBackground.ClipContents = true; 
		mapBackground.GuiInput += OnMapGuiInput;
		uiContainer.AddChild(mapBackground);

		mapCanvas = new Control();
		mapCanvas.Size = new Vector2(400, 320);
		mapBackground.AddChild(mapCanvas);

		Label label = new Label();
		label.Text = "PLANTA DO NÍVEL\n(Arrastar: Rato | Zoom: Scroll)";
		label.Position = new Vector2(10, 10);
		label.LabelSettings = new LabelSettings() { FontSize = 12, FontColor = new Color(0.4f, 0.8f, 1.0f) };
		uiContainer.AddChild(label);

		DrawFullTopology2D();
		ApplyMapTransform();
	}

	private void OnMapGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Middle || mb.ButtonIndex == MouseButton.Left)
				isDraggingMap = mb.Pressed;
			
			if (mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
			{
				float oldZoom = mapZoom;
				if (mb.ButtonIndex == MouseButton.WheelUp) mapZoom = Mathf.Min(mapZoom * 1.2f, 4.0f);
				if (mb.ButtonIndex == MouseButton.WheelDown) mapZoom = Mathf.Max(mapZoom / 1.2f, 0.2f);
				
				// Matemática de Zoom Local exata (centra na seta do rato)
				Vector2 mousePos = mapBackground.GetLocalMousePosition();
				Vector2 localPos = (mousePos - mapPan) / oldZoom;
				mapPan = mousePos - localPos * mapZoom;
				
				ApplyMapTransform();
			}
		}
		else if (@event is InputEventMouseMotion motion && isDraggingMap)
		{
			mapPan += motion.Relative;
			ApplyMapTransform();
		}
	}

	private void ApplyMapTransform()
	{
		mapCanvas.Scale = new Vector2(mapZoom, mapZoom);
		mapCanvas.Position = mapPan;
	}

	private void DrawFullTopology2D()
	{
		float cx = 200f; 
		float cy = 160f;
		float spacing = 90f; 
		float iconSize = 26f; 

		DrawTopologicalView(Vector3.Forward, Vector3.Right, Vector3.Up, new Vector2(cx, cy), iconSize);
		DrawTopologicalView(Vector3.Back, Vector3.Left, Vector3.Up, new Vector2(cx + spacing * 3, cy), iconSize);
		DrawTopologicalView(Vector3.Right, Vector3.Back, Vector3.Up, new Vector2(cx + spacing, cy), iconSize);
		DrawTopologicalView(Vector3.Left, Vector3.Forward, Vector3.Up, new Vector2(cx - spacing, cy), iconSize);
		DrawTopologicalView(Vector3.Up, Vector3.Right, Vector3.Back, new Vector2(cx, cy - spacing), iconSize);
		DrawTopologicalView(Vector3.Down, Vector3.Right, Vector3.Forward, new Vector2(cx, cy + spacing), iconSize);
	}

	private void DrawTopologicalView(Vector3 viewDir, Vector3 layoutRight, Vector3 layoutUp, Vector2 centerPos, float iconSize)
	{
		Dictionary<Vector2I, (float depth, FaceType face)> projection = new Dictionary<Vector2I, (float, FaceType)>();

		foreach (var kvp in generatedFaces)
		{
			Vector3I pos = kvp.Key;
			int x = Mathf.RoundToInt(new Vector3(pos.X, pos.Y, pos.Z).Dot(layoutRight));
			int y = Mathf.RoundToInt(new Vector3(pos.X, pos.Y, pos.Z).Dot(layoutUp));
			float depth = new Vector3(pos.X, pos.Y, pos.Z).Dot(viewDir); 

			Vector2I coord = new Vector2I(x, y);
			
			if (!projection.ContainsKey(coord) || projection[coord].depth < depth)
			{
				FaceType visibleFace = GetFaceFromDirection(kvp.Value, viewDir);
				projection[coord] = (depth, visibleFace);
			}
		}

		float minX = float.MaxValue, maxX = float.MinValue;
		float minY = float.MaxValue, maxY = float.MinValue;
		foreach (var coord in projection.Keys) { if (coord.X < minX) minX = coord.X; if (coord.X > maxX) maxX = coord.X; if (coord.Y < minY) minY = coord.Y; if (coord.Y > maxY) maxY = coord.Y; }

		float offsetX = -(maxX + minX) / 2f;
		float offsetY = -(maxY + minY) / 2f;

		foreach (var kvp in projection)
		{
			Vector2I coord = kvp.Key;
			FaceType face = kvp.Value.face;

			float screenX = centerPos.X + (coord.X + offsetX) * iconSize - iconSize / 2f;
			float screenY = centerPos.Y - (coord.Y + offsetY) * iconSize - iconSize / 2f;

			TextureRect icon = new TextureRect();
			icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			icon.Position = new Vector2(screenX, screenY);
			icon.Size = new Vector2(iconSize, iconSize);
			icon.Texture = GetFaceTexture(face);

			ColorRect bg = new ColorRect();
			bg.Color = new Color(0.1f, 0.3f, 0.5f, 0.8f);
			bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			bg.ShowBehindParent = true;
			icon.AddChild(bg);

			mapCanvas.AddChild(icon); 
		}
	}

	private FaceType GetFaceFromDirection(FaceType[] faces, Vector3 dir)
	{
		if (dir == Vector3.Up) return faces[0];
		if (dir == Vector3.Down) return faces[1];
		if (dir == Vector3.Right) return faces[2];
		if (dir == Vector3.Left) return faces[3];
		if (dir == Vector3.Forward) return faces[4];
		if (dir == Vector3.Back) return faces[5];
		return FaceType.Flat;
	}

	private Texture2D GetFaceTexture(FaceType type)
	{
		switch (type)
		{
			case FaceType.QuadExt: return texQuadExt;
			case FaceType.QuadInt: return texQuadInt;
			case FaceType.CircExt: return texCircExt;
			case FaceType.CircInt: return texCircInt;
			case FaceType.CruzExt: return texCruzExt;
			case FaceType.CruzInt: return texCruzInt;
			default: return texFlat;
		}
	}
}
