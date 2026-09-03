// ============================================================================
// SlashArc.fx  刀光扇形带（本仓自制，参考学习自 CalamityOverhaul（MIT, (c) hocha113）
//   Assets/Effects/OniGateRift.fx 的机制：顶点带 uv.x=沿刃/uv.y=径向(0内缘软..1外缘锐)，
//   像素侧做 揭开(uHead)+刃头白热线(uLead)+径向三层截面+色阶+预乘alpha。
//   本文件为自主精简实现，未复制其噪声/笔刷通道。MIT 声明见 写法\开源参考与合规规范.md。
// ============================================================================
float4x4 transformMatrix;
float uHead = 1.0;    // 刃头揭开位置(0..1)，我们每帧只生成到当前进度，头=最新
float uLead = 1.0;    // 刃头白热线强度
float uFlash = 0.0;   // 落位闪
float uOpacity = 1.0; // 整体不透明度(含收势沉降)
float3 uColTint;      // 主色(旧段)
float3 uColDeep;      // 主色暗阶(内缘底)
float3 uColHot;       // 白热(刃头/核心)

struct VSInput { float4 Position : POSITION0; float2 TexCoords : TEXCOORD0; float4 Color : COLOR0; };
struct PSInput { float4 Position : POSITION0; float2 TexCoords : TEXCOORD0; float4 Color : COLOR0; };

PSInput VS(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.TexCoords = v.TexCoords;
    o.Color = v.Color;
    return o;
}

// 沿刃软羽(窗)：两端都软，0 起笔 / 1 收笔
float WinFeather(float uc)
{
    return smoothstep(-0.02, 0.06, uc) * (1.0 - smoothstep(0.92, 1.05, uc));
}

float4 PS(PSInput i) : COLOR0
{
    float uc = i.TexCoords.x;
    float v  = i.TexCoords.y;
    float wf = WinFeather(uc);
    if (wf * i.Color.a < 0.003) return float4(0,0,0,0);

    // 揭开：头(uHead)之后不存在；刃头白热亮线(高斯) = “刀在这里”
    float reveal = smoothstep(uHead + 0.012, uHead - 0.06, uc);
    float lead = exp(-pow((uc - uHead) / 0.028, 2.0)) * uLead;
    if (reveal + lead < 0.006) return float4(0,0,0,0);

    // 径向三层截面(阔剑式)：内缘软融 → 体 → 外缘锐利，白热核贴外缘(v≈0.9)
    float innerFade = smoothstep(0.0, 0.30, v);
    float outerCut  = 1.0 - smoothstep(0.955, 1.0, v);
    float core = exp(-pow((v - 0.90) / 0.055, 2.0));
    float bodyA = innerFade * outerCut * (0.40 + core * 0.45);

    // 色阶：内缘暗阶 → 主色(随 uc 向头走亮) → 白热(核 + 刃头线 + 闪)
    float along = smoothstep(0.0, 1.0, uc);
    float3 col = lerp(uColDeep, uColTint, smoothstep(0.05, 0.75, v));
    col = lerp(col, uColHot, along * 0.45);
    col += uColHot * core * 0.90;
    col += uColHot * lead * 1.7;
    col += uColHot * uFlash * 0.30 * (0.4 + core * 0.6);

    // alpha：体 × 揭开 + 刃头线；预乘输出
    float alpha = saturate(bodyA * reveal + lead * 0.55 * innerFade);
    alpha *= wf * uOpacity * i.Color.a;
    return float4(col * alpha, alpha);
}

technique BandTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}