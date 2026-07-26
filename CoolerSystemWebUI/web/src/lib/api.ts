import type { AircondState, AutomationConfig, CirculatorConfig, SensorReading } from './types';

/**
 * バックエンド (ASP.NET) の /api/* を叩く。ASP.NET はこれを RPi の AEHAFmtSender へそのまま中継する。
 * 認証は Cloudflare Access が前段で処理するため、ここでは何も付けない。
 * ただし Access のセッション Cookie を載せるために credentials: 'same-origin' は必要。
 */

const BASE = '/api';

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T | undefined> {
  let res: Response;
  try {
    res = await fetch(`${BASE}/${path}`, {
      credentials: 'same-origin',
      cache: 'no-store',
      ...init,
    });
  } catch (e) {
    throw new ApiError('サーバに接続できません', 0);
  }

  if (!res.ok) {
    throw new ApiError(await describe(res), res.status);
  }

  const text = await res.text();
  if (!text) return undefined;
  try {
    return JSON.parse(text) as T;
  } catch {
    // /apiac や /simplecode は JSON でない文字列 ("OK" など) を返すことがある。
    return undefined;
  }
}

async function describe(res: Response): Promise<string> {
  if (res.status === 502) return 'ラズパイに接続できません';
  if (res.status === 504) return 'ラズパイからの応答がありません';
  const body = await res.text().catch(() => '');
  return body ? `エラー (${res.status}): ${body.slice(0, 120)}` : `エラー (${res.status})`;
}

function postJson(path: string, body: unknown): Promise<unknown> {
  return request(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
}

export const api = {
  getState: () => request<AircondState>('acget') as Promise<AircondState>,
  apply: (state: AircondState) => postJson('apiac', state),

  sendCirculator: (id: string) => postJson('simplecode', { id }),
  getCirculatorConfig: () => request<CirculatorConfig>('circulatorconfig') as Promise<CirculatorConfig>,

  getAutomation: () => request<AutomationConfig>('automationconfig') as Promise<AutomationConfig>,
  setAutomation: (config: AutomationConfig) => postJson('automationconfig', config),

  getLatestSensor: () => request<SensorReading>('sensordata/latest'),
  getSensorHistory: (minutes = 1440, limit = 500) =>
    request<SensorReading[]>(`sensordata/history?minutes=${minutes}&limit=${limit}`),
};
