// Adapted from Frank Luna's "Sprites and Text" example here: http://www.d3dcoder.net/resources.htm 
// checkout his books here: http://www.d3dcoder.net/default.htm

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Color = System.Drawing.Color;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace Capture.Hook.DX11
{
    public class DXSprite : Component
    {
        private readonly Device device;
        private readonly DeviceContext deviceContext;

        public DXSprite(Device device, DeviceContext deviceContext)
        {
            this.device = device;
            this.deviceContext = deviceContext;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SpriteVertex
        {
            public Vector3 Pos;
            public Vector2 Tex;
            public Color4 Color;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Sprite
        {
            public readonly Rectangle SrcRect;
            public readonly Rectangle DestRect;
            public readonly Color4 Color;
            public float Z;
            public float Angle;
            public float Scale;

            public Sprite(Rectangle sourceRect, Rectangle destRect, Color4 color)
            {
                SrcRect = sourceRect;
                DestRect = destRect;
                Color = color;
                Z = 0.0f;
                Angle = 0.0f;
                Scale = 1.0f;
            }
        }

        private readonly List<Sprite> spriteList = new List<Sprite>(128);
        
        private bool initialized;
        private BlendState transparentBs;
        private EffectTechnique spriteTech;
        private EffectShaderResourceVariable spriteMap;
        private ShaderResourceView batchTexSrv;
        private InputLayout inputLayout;
        private Buffer vb;
        private Buffer ib;
        private int texWidth;
        private int texHeight;
        private float screenWidth;
        private float screenHeight;
        private CompilationResult compiledFx;
        private Effect effect;

        private SafeHGlobal indexBuffer;
        public bool Initialize()
        {
            Debug.Assert(!initialized);

            #region Shaders
            const string spriteFx = @"Texture2D SpriteTex;
SamplerState samLinear {
    Filter = MIN_MAG_MIP_LINEAR;
    AddressU = WRAP;
    AddressV = WRAP;
};
struct VertexIn {
    float3 PosNdc : POSITION;
    float2 Tex    : TEXCOORD;
    float4 Color  : COLOR;
};
struct VertexOut {
    float4 PosNdc : SV_POSITION;
    float2 Tex    : TEXCOORD;
    float4 Color  : COLOR;
};
VertexOut VS(VertexIn vin) {
    VertexOut vOut;
    vOut.PosNdc = float4(vin.PosNdc, 1.0f);
    vOut.Tex    = vin.Tex;
    vOut.Color  = vin.Color;
    return vOut;
};
float4 PS(VertexOut pin) : SV_Target {
    return pin.Color*SpriteTex.Sample(samLinear, pin.Tex);
};
technique11 SpriteTech {
    pass P0 {
        SetVertexShader( CompileShader( vs_5_0, VS() ) );
        SetHullShader( NULL );
        SetDomainShader( NULL );
        SetGeometryShader( NULL );
        SetPixelShader( CompileShader( ps_5_0, PS() ) );
    }
};";
            #endregion

            compiledFx = ToDispose(ShaderBytecode.Compile(spriteFx, "SpriteTech", "fx_5_0"));
            {
                if (compiledFx.HasErrors)
                    return false;

                effect = ToDispose(new Effect(device, compiledFx));
                {
                    spriteTech = ToDispose(effect.GetTechniqueByName("SpriteTech"));
                    spriteMap = ToDispose(effect.GetVariableByName("SpriteTex").AsShaderResource());

                    using (var pass = spriteTech.GetPassByIndex(0))
                    {
                        InputElement[] layoutDesc = 
                        {
                            new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                            new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0, InputClassification.PerVertexData, 0),
                            new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 20, 0, InputClassification.PerVertexData, 0)
                        };

                        inputLayout = ToDispose(new InputLayout(device, pass.Description.Signature, layoutDesc));
                    }
                    // Create Vertex Buffer
                    var vbd = new BufferDescription
                    {
                        SizeInBytes = 2048 * Marshal.SizeOf(typeof(SpriteVertex)),
                        Usage = ResourceUsage.Dynamic,
                        BindFlags = BindFlags.VertexBuffer,
                        CpuAccessFlags = CpuAccessFlags.Write,
                        OptionFlags = ResourceOptionFlags.None,
                        StructureByteStride = 0
                    };

                    vb = ToDispose(new Buffer(device, vbd));

                    // Create and initialise Index Buffer

                    var indices = new short[3072];

                    for (ushort i = 0; i < 512; ++i)
                    {
                        indices[i * 6] = (short)(i * 4);
                        indices[i * 6 + 1] = (short)(i * 4 + 1);
                        indices[i * 6 + 2] = (short)(i * 4 + 2);
                        indices[i * 6 + 3] = (short)(i * 4);
                        indices[i * 6 + 4] = (short)(i * 4 + 2);
                        indices[i * 6 + 5] = (short)(i * 4 + 3);
                    }

                    indexBuffer = ToDispose(new SafeHGlobal(indices.Length * Marshal.SizeOf(indices[0])));
                    Marshal.Copy(indices, 0, indexBuffer.DangerousGetHandle(), indices.Length);

                    var ibd = new BufferDescription
                    {
                        SizeInBytes = 3072 * Marshal.SizeOf(typeof(short)),
                        Usage = ResourceUsage.Immutable,
                        BindFlags = BindFlags.IndexBuffer,
                        CpuAccessFlags = CpuAccessFlags.None,
                        OptionFlags = ResourceOptionFlags.None,
                        StructureByteStride = 0
                    };
                    
                    ib = ToDispose(new Buffer(device, indexBuffer.DangerousGetHandle(), ibd));

                    var transparentDesc = new BlendStateDescription
                    {
                        AlphaToCoverageEnable = false,
                        IndependentBlendEnable = false,
                    };
                    transparentDesc.RenderTarget[0].IsBlendEnabled = true;
                    transparentDesc.RenderTarget[0].SourceBlend = BlendOption.SourceAlpha;
                    transparentDesc.RenderTarget[0].DestinationBlend = BlendOption.InverseSourceAlpha;
                    transparentDesc.RenderTarget[0].BlendOperation = BlendOperation.Add;
                    transparentDesc.RenderTarget[0].SourceAlphaBlend = BlendOption.One;
                    transparentDesc.RenderTarget[0].DestinationAlphaBlend = BlendOption.Zero;
                    transparentDesc.RenderTarget[0].AlphaBlendOperation = BlendOperation.Add;
                    transparentDesc.RenderTarget[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;

                    transparentBs = ToDispose(new BlendState(device, transparentDesc));
                }
            }

            initialized = true;

            return true;
        }

        private static Color4 ToColor4(Color color)
        {
            var vec = new Vector4(color.R > 0 ? color.R / 255.0f : 0.0f, 
                color.G > 0 ? color.G / 255.0f : 0.0f,
                color.B > 0 ? color.B / 255.0f : 0.0f, 
                color.A > 0 ? color.A / 255.0f : 0.0f);
            
            return new Color4(vec);
        }

        public void DrawImage(int x, int y, float scale, float angle, Color? color, DXImage image)
        {
            Debug.Assert(initialized);

            var blendFactor = new Color4(1.0f);
            using (var backupBlendState = deviceContext.OutputMerger.GetBlendState(out var backupBlendFactor, out var backupMask))
            {
                deviceContext.OutputMerger.SetBlendState(transparentBs, blendFactor);

                BeginBatch(image.GetSrv());

                Draw(new Rectangle(x, y, (int)(scale * image.Width), (int)(scale * image.Height)), new Rectangle(0, 0, image.Width, image.Height), color.HasValue ? ToColor4(color.Value) : Color4.White, 1.0f, angle);

                EndBatch();
                deviceContext.OutputMerger.SetBlendState(backupBlendState, backupBlendFactor, backupMask);
            }
        }

        public void DrawString(int x, int y, string text, Color color, DXFont f)
        {
            var blendFactor = new Color4(1.0f);
            using (var backupBlendState = deviceContext.OutputMerger.GetBlendState(out var backupBlendFactor, out var backupMask))
            {
                deviceContext.OutputMerger.SetBlendState(transparentBs, blendFactor);

                BeginBatch(f.GetFontSheetSrv());

                var length = text.Length;

                var posX = x;
                var posY = y;

                var color4 = ToColor4(color);

                for (var i = 0; i < length; ++i)
                {
                    var character = text[i];

                    switch (character)
                    {
                        case ' ':
                            posX += f.GetSpaceWidth();
                            break;
                        
                        case '\n':
                            posX = x;
                            posY += f.GetCharHeight();
                            break;
                        
                        default:
                        {
                            var charRect = f.GetCharRect(character);

                            var width = charRect.Right - charRect.Left;
                            var height = charRect.Bottom - charRect.Top;

                            Draw(new Rectangle(posX, posY, width, height), charRect, color4);

                            posX += width + 1;
                            break;
                        }
                    }
                }

                EndBatch();
                deviceContext.OutputMerger.SetBlendState(backupBlendState, backupBlendFactor, backupMask);
            }
        }

        private void BeginBatch(ShaderResourceView texSrv)
        {
            Debug.Assert(initialized);

            batchTexSrv = texSrv;

            var tex = batchTexSrv.ResourceAs<Texture2D>();
            {

                var texDesc = tex.Description;
                texWidth = texDesc.Width;
                texHeight = texDesc.Height;
            }
            spriteList.Clear();
        }

        private void EndBatch()
        {
            Debug.Assert(initialized);

            var vp = deviceContext.Rasterizer.GetViewports<ViewportF>();

            screenWidth = vp[0].Width;
            screenHeight = vp[0].Height;

            var stride = Marshal.SizeOf(typeof(SpriteVertex));
            var offset = 0;
            deviceContext.InputAssembler.InputLayout = inputLayout;
            deviceContext.InputAssembler.SetIndexBuffer(ib, Format.R16_UInt, 0);
            deviceContext.InputAssembler.SetVertexBuffers(0, new[] { vb }, new[] { stride }, new[] { offset });
            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            spriteMap.SetResource(batchTexSrv);

            using (var pass = spriteTech.GetPassByIndex(0))
            {
                pass.Apply(deviceContext);
                var spritesToDraw = spriteList.Count;
                var startIndex = 0;
                while (spritesToDraw > 0)
                {
                    if (spritesToDraw <= 512)
                    {
                        DrawBatch(startIndex, spritesToDraw);
                        spritesToDraw = 0;
                    }
                    else
                    {
                        DrawBatch(startIndex, 512);
                        startIndex += 512;
                        spritesToDraw -= 512;
                    }
                }
            }
            batchTexSrv = null;
        }

        private void Draw(Rectangle destinationRect, Rectangle sourceRect, Color4 color, float scale = 1.0f, float angle = 0f, float z = 0f)
        {
            var sprite = new Sprite(
                sourceRect,
                destinationRect,
                color
            )
            {
                Scale = scale,
                Angle = angle,
                Z = z
            };

            spriteList.Add(sprite);
        }

        private void DrawBatch(int startSpriteIndex, int spriteCount)
        {
            var mappedData = deviceContext.MapSubresource(vb, 0, MapMode.WriteDiscard, MapFlags.None);

            // Update the vertices
            unsafe
            {
                var v = (SpriteVertex*)mappedData.DataPointer.ToPointer();

                for (var i = 0; i < spriteCount; ++i)
                {
                    var sprite = spriteList[startSpriteIndex + i];

                    var quad = new SpriteVertex[4];

                    BuildSpriteQuad(sprite, ref quad);

                    v[i * 4] = quad[0];
                    v[i * 4 + 1] = quad[1];
                    v[i * 4 + 2] = quad[2];
                    v[i * 4 + 3] = quad[3];
                }
            }

            deviceContext.UnmapSubresource(vb, 0);

            deviceContext.DrawIndexed(spriteCount * 6, 0, 0);
        }

        private Vector3 PointToNdc(int x, int y, float z)
        {
            Vector3 p;

            p.X = 2.0f * x / screenWidth - 1.0f;
            p.Y = 1.0f - 2.0f * y / screenHeight;
            p.Z = z;

            return p;
        }

        private void BuildSpriteQuad(Sprite sprite, ref SpriteVertex[] v)
        {
            if (v.Length < 4)
                throw new ArgumentException(@"Must have 4 sprite vertices", nameof(v));

            var dest = sprite.DestRect;
            var src = sprite.SrcRect;

            v[0].Pos = PointToNdc(dest.Left, dest.Bottom, sprite.Z);
            v[1].Pos = PointToNdc(dest.Left, dest.Top, sprite.Z);
            v[2].Pos = PointToNdc(dest.Right, dest.Top, sprite.Z);
            v[3].Pos = PointToNdc(dest.Right, dest.Bottom, sprite.Z);

            v[0].Tex = new Vector2((float)src.Left / texWidth, (float)src.Bottom / texHeight);
            v[1].Tex = new Vector2((float)src.Left / texWidth, (float)src.Top / texHeight);
            v[2].Tex = new Vector2((float)src.Right / texWidth, (float)src.Top / texHeight);
            v[3].Tex = new Vector2((float)src.Right / texWidth, (float)src.Bottom / texHeight);

            v[0].Color = sprite.Color;
            v[1].Color = sprite.Color;
            v[2].Color = sprite.Color;
            v[3].Color = sprite.Color;

            var tx = 0.5f * (v[0].Pos.X + v[3].Pos.X);
            var ty = 0.5f * (v[0].Pos.Y + v[1].Pos.Y);

            var origin = new Vector2(tx, ty);
            var translation = new Vector2(0.0f, 0.0f);

            var T = Matrix.AffineTransformation2D(sprite.Scale, origin, sprite.Angle, translation);

            for (var i = 0; i < 4; ++i)
            {
                var p = v[i].Pos;
                p = Vector3.TransformCoordinate(p, T);
                v[i].Pos = p;
            }
        }
    }
}

