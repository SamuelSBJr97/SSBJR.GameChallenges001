using Godot;
using System;

public partial class OrbitCamera
{
	private void Build3DMinimapUI()
	{
		CanvasLayer canvas = new CanvasLayer();
		AddChild(canvas);

		helpLabel = new Label();
		helpLabel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		helpLabel.Position = new Vector2(20, 20);
		helpLabel.LabelSettings = new LabelSettings() { FontSize = 16, FontColor = Colors.White };
		canvas.AddChild(helpLabel);

		SubViewportContainer container = new SubViewportContainer();
		container.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		container.Position = new Vector2(-280, 20);
		container.Size = new Vector2(250, 250);
		canvas.AddChild(container);

		ColorRect bg = new ColorRect();
		bg.Color = new Color(0, 0, 0, 0.5f);
		bg.Size = new Vector2(250, 250);
		container.AddChild(bg);

		minimapViewport = new SubViewport();
		minimapViewport.TransparentBg = true;
		minimapViewport.Size = new Vector2I(250, 250);
		minimapViewport.OwnWorld3D = true; 
		container.AddChild(minimapViewport);

		minimapRoot = new Node3D();
		minimapViewport.AddChild(minimapRoot);

		minimapCamera = new Camera3D();
		minimapViewport.AddChild(minimapCamera);

		DirectionalLight3D light1 = new DirectionalLight3D();
		light1.Position = new Vector3(5, 5, 5);
		light1.LookAt(Vector3.Zero);
		minimapViewport.AddChild(light1);

		DirectionalLight3D light2 = new DirectionalLight3D();
		light2.Position = new Vector3(-5, 5, -5);
		light2.LookAt(Vector3.Zero);
		minimapViewport.AddChild(light2);
	}

	private void UpdateUIProcess()
	{
		Node3D activeNode = isHoldingCore ? coreGroup : selectedPiece;

		if (activeNode == null)
		{
			helpLabel.Text = "=== CONTROLES ===\nClique Esq: Selecionar\nDir: Voar Câmera";
			minimapRoot.Visible = false;
		}
		else
		{
			helpLabel.Text = isHoldingCore ? "=== MOVENDO BLOCO (Clique para fixar) ===" : "=== ROTACIONAR (Setas / PgUp / PgDn) ===";
			minimapRoot.Visible = true;

			minimapRoot.Basis = activeNode.GlobalTransform.Basis;

			Vector3 dirToCam = (camera.GlobalPosition - activeNode.GlobalPosition).Normalized();
			float zoomDist = isHoldingCore ? 8.0f + (coreGroup.GetChildCount() * 1.5f) : 6.0f;
			
			minimapCamera.Position = dirToCam * zoomDist;
			minimapCamera.LookAt(Vector3.Zero);
		}
	}

	private void RefreshMinimap()
	{
		foreach (Node n in minimapRoot.GetChildren()) n.QueueFree();

		Node3D activeNode = isHoldingCore ? coreGroup : selectedPiece;
		if (activeNode == null) return;

		Transform3D invTransform = activeNode.GlobalTransform.AffineInverse();
		CloneMeshes(activeNode, minimapRoot, invTransform);
	}

	private void CloneMeshes(Node current, Node targetParent, Transform3D invTransform)
	{
		if (current is MeshInstance3D meshInst)
		{
			MeshInstance3D clone = new MeshInstance3D();
			clone.Mesh = meshInst.Mesh;
			
			if (meshInst.MaterialOverride != null) clone.MaterialOverride = meshInst.MaterialOverride;
			for (int i = 0; i < meshInst.GetSurfaceOverrideMaterialCount(); i++)
				clone.SetSurfaceOverrideMaterial(i, meshInst.GetSurfaceOverrideMaterial(i));

			clone.Transform = invTransform * meshInst.GlobalTransform;
			targetParent.AddChild(clone);
		}

		foreach (Node child in current.GetChildren())
			CloneMeshes(child, targetParent, invTransform);
	}
}
