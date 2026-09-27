#!/usr/bin/env node
/**
 * ask-agy.mjs —— AGY（Gemini 站）咨询调用器
 * =====================================================================
 * 依据：《写法\AI工具配置与调用纪律.md》§一。纪律硬编码在此，不靠人记：
 *   · 串行  —— 锁文件保护，同一时刻只允许一个调用（用户红线：禁止并发调用）
 *   · 输入  —— 单次 ≤ 80k token，超限直接拒绝，**不截断**
 *   · 输出  —— max_tokens 固定 500
 *   · 重试  —— 失败最多换高档重试 1 次，禁止重试风暴
 *   · 密钥  —— 只从 .secrets.local.json 读取；不打印、不写回、不进日志
 *
 * 为什么不是 PowerShell：本机 curl/PS 都走 Windows schannel，而沙箱禁止进程访问
 * 证书库（SEC_E_NO_CREDENTIALS / "underlying connection was closed"），HTTPS 必然失败。
 * Node 自带 OpenSSL，直连与走本地代理（127.0.0.1:7897）均已实测 200。
 *
 * 用法：
 *   node .\写法\_tools\ask-agy.mjs --prompt .\写法\_agy\prompt_01.md --out .\写法\_agy\reply_01.md
 *   可选：--model agy-gemini-3.8-flash-medium|high  --retry-high  --max-tokens 500
 */

import fs from 'node:fs';
import path from 'node:path';
import https from 'node:https';
import http from 'node:http';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, '..', '..');           // 写法\_tools -> 工作区根

// ---------------- 参数 ----------------
const argv = process.argv.slice(2);
const arg = (name, def = null) => {
  const i = argv.indexOf(name);
  return i >= 0 && i + 1 < argv.length ? argv[i + 1] : def;
};
const has = (name) => argv.includes(name);

const promptFile = arg('--prompt');
const outFile = arg('--out');
let model = arg('--model', 'agy-gemini-3.8-flash-low');
const maxTokens = Number(arg('--max-tokens', '500'));
const retryHigh = has('--retry-high');
const MAX_INPUT_CHARS = 240000;   // ≈80k token 的保守上限（中文留足余量）

if (!promptFile || !outFile) {
  console.error('用法: node ask-agy.mjs --prompt <in.md> --out <out.md> [--model ...] [--retry-high]');
  process.exit(2);
}

// ---------------- 串行锁（禁止并发） ----------------
// 实测坑（2026-09-27）：本沙箱下 Node 的 fs.rmSync 会返回"成功"但文件依然存在
// （pwsh 的 Remove-Item 可以删，Node 不行）。因此锁**不依赖删除**：
//   判活 = 读锁里的 PID → process.kill(pid, 0) 探活。释放 = 把 PID 改写成 0（0 永不存活）。
const agyDir = path.join(ROOT, '写法', '_agy');
fs.mkdirSync(agyDir, { recursive: true });
const lockPath = path.join(agyDir, '.ask-agy.lock');

function pidAlive(pid) {
  if (!pid || pid <= 0) return false;
  try { process.kill(pid, 0); return true; }
  catch (e) { return e.code === 'EPERM'; }   // EPERM = 进程存在但非本用户；ESRCH = 已死
}

if (fs.existsSync(lockPath)) {
  const holder = parseInt(String(fs.readFileSync(lockPath, 'utf8')).trim(), 10) || 0;
  if (pidAlive(holder)) {
    console.error(`拒绝调用：已有 AGY 调用在进行中（pid=${holder} 仍存活）。纪律：禁止并发调用。`);
    process.exit(3);
  }
  fs.rmSync(lockPath, { force: true });   // 尽力而为；删不掉也无妨，下面会覆盖 PID
}
fs.writeFileSync(lockPath, String(process.pid), 'utf8');
const release = () => { try { fs.writeFileSync(lockPath, '0', 'utf8'); } catch {} };
process.on('exit', release);

// ---------------- 读密钥 ----------------
const secretsPath = path.join(ROOT, '.secrets.local.json');
const secrets = JSON.parse(fs.readFileSync(secretsPath, 'utf8'));
const { base, key } = secrets.gemini_station;
if (!key) { console.error('密钥字段为空。'); process.exit(4); }

// ---------------- 读 prompt ----------------
const prompt = fs.readFileSync(path.resolve(ROOT, promptFile), 'utf8');
if (prompt.length > MAX_INPUT_CHARS) {
  console.error(`输入超限：${prompt.length} 字符 > ${MAX_INPUT_CHARS}。纪律：≤80k token，不截断。`);
  process.exit(5);
}

// ---------------- 传输 ----------------
// 用户环境开着 TUN 虚拟网卡，**直连优先**；本地代理（Clash 7897）只作回落。
// 可用 --via-proxy 强制走代理。
function probeProxy() {
  return new Promise((resolve) => {
    const req = http.request({ host: '127.0.0.1', port: 7897, method: 'CONNECT', path: 'www.example.com:443', timeout: 1500 });
    req.on('connect', (res, socket) => { socket.destroy(); resolve(res.statusCode === 200); });
    req.on('timeout', () => { req.destroy(); resolve(false); });
    req.on('error', () => resolve(false));
    req.end();
  });
}

function post(url, bodyObj, useProxy) {
  return new Promise((resolve, reject) => {
    const u = new URL(url);
    const payload = Buffer.from(JSON.stringify(bodyObj), 'utf8');
    const headers = {
      'Authorization': `Bearer ${key}`,
      'Content-Type': 'application/json; charset=utf-8',
      'Content-Length': payload.length,
    };
    const finish = (res) => {
      const chunks = [];
      res.on('data', (c) => chunks.push(c));
      res.on('end', () => {
        const text = Buffer.concat(chunks).toString('utf8');
        if (res.statusCode < 200 || res.statusCode >= 300) {
          reject(new Error(`HTTP ${res.statusCode}: ${text.slice(0, 300)}`));
        } else {
          try { resolve(JSON.parse(text)); } catch (e) { reject(new Error(`响应非 JSON: ${text.slice(0, 200)}`)); }
        }
      });
    };

    if (!useProxy) {
      const req = https.request({ hostname: u.hostname, port: 443, path: u.pathname + u.search, method: 'POST', headers, timeout: 180000 }, finish);
      req.on('timeout', () => { req.destroy(new Error('timeout')); });
      req.on('error', reject);
      req.end(payload);
      return;
    }
    // 经 CONNECT 隧道
    const cr = http.request({ host: '127.0.0.1', port: 7897, method: 'CONNECT', path: `${u.hostname}:443`, timeout: 15000 });
    cr.on('connect', (res, socket) => {
      if (res.statusCode !== 200) { reject(new Error(`代理 CONNECT ${res.statusCode}`)); return; }
      const req = https.request({ hostname: u.hostname, path: u.pathname + u.search, method: 'POST', headers, socket, agent: false, timeout: 180000 }, finish);
      req.on('timeout', () => req.destroy(new Error('timeout')));
      req.on('error', reject);
      req.end(payload);
    });
    cr.on('timeout', () => cr.destroy(new Error('代理 timeout')));
    cr.on('error', reject);
    cr.end();
  });
}

// ---------------- 主流程 ----------------
(async () => {
  try {
    await run();
  } finally {
    release();
  }
})();

async function run() {
  const forceProxy = has('--via-proxy');
  let useProxy = false;
  if (forceProxy) {
    useProxy = await probeProxy();
    if (!useProxy) { console.error('指定了 --via-proxy 但 127.0.0.1:7897 不可用。'); process.exitCode = 1; return; }
  }
  const endpoint = `${base}/chat/completions`;

  const models = [model];
  if (retryHigh) models.push('agy-gemini-3.8-flash-medium');

  let lastErr = null;
  for (const m of models) {
    const body = {
      model: m,
      messages: [{ role: 'user', content: prompt }],
      max_tokens: maxTokens,
      temperature: 0.7,
    };
    try {
      const resp = await post(endpoint, body, useProxy);
      const content = resp.choices?.[0]?.message?.content ?? '';
      const u = resp.usage ?? {};
      const head = `<!-- AGY 回执 | model=${m} | via=${useProxy ? 'proxy:7897' : 'direct'} | prompt_tokens=${u.prompt_tokens} | completion_tokens=${u.completion_tokens} | 生成于 ${new Date().toISOString()} -->\n\n`;
      fs.writeFileSync(path.resolve(ROOT, outFile), head + content, 'utf8');
      console.log(`OK  model=${m}  via=${useProxy ? 'proxy' : 'direct'}  prompt_tokens=${u.prompt_tokens}  completion_tokens=${u.completion_tokens}  -> ${outFile}`);
      return;
    } catch (e) {
      lastErr = e;
      console.log(`FAIL model=${m} via=${useProxy ? 'proxy' : 'direct'}  :: ${e.message}`);
      // 直连失败 → 回落到本地代理（传输回落，不是模型重试）
      if (!useProxy && !forceProxy && await probeProxy()) {
        useProxy = true;
        try {
          const resp = await post(endpoint, body, true);
          const content = resp.choices?.[0]?.message?.content ?? '';
          const u = resp.usage ?? {};
          const head = `<!-- AGY 回执 | model=${m} | via=proxy:7897(回落) | prompt_tokens=${u.prompt_tokens} | completion_tokens=${u.completion_tokens} | 生成于 ${new Date().toISOString()} -->\n\n`;
          fs.writeFileSync(path.resolve(ROOT, outFile), head + content, 'utf8');
          console.log(`OK  model=${m}  via=proxy(fallback)  prompt_tokens=${u.prompt_tokens}  completion_tokens=${u.completion_tokens}  -> ${outFile}`);
          return;
        } catch (e2) { lastErr = e2; console.log(`FAIL model=${m} via=proxy  :: ${e2.message}`); }
      }
    }
  }
  console.error(`AGY 调用失败（已按纪律停止，不做重试风暴）：${lastErr?.message}`);
  process.exitCode = 1;
}
