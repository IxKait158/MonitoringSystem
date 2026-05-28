import http from 'k6/http';
import { check } from 'k6';
import { randomIntBetween } from 'https://jslib.k6.io/k6-utils/1.2.0/index.js';

const API_URL = __ENV.API_URL || 'https://localhost:7246';
const API_KEY = __ENV.API_KEY || 'mk_e9e7751b9e4b414a8f83a525663bcba50';
const VUS = parseInt(__ENV.VUS || '10');

export const options = {
    vus: VUS,                    // кількість віртуальних користувачів
    duration: '30s',             // тривалість тесту
    insecureSkipTLSVerify: true, // для localhost із самопідписаним сертифікатом
    thresholds: {
        http_req_failed: ['rate<0.01'],     // менш ніж 1% помилок
        http_req_duration: ['p(95)<500'],   // 95% запитів швидше 500мс
    },
};

export default function () {
    const payload = JSON.stringify({
        serviceName: 'LoadTestService',
        metrics: [
            {
                serviceName: 'LoadTestService',
                metricName: 'system.cpu_percent',
                value: randomIntBetween(10, 80),
                timestamp: new Date().toISOString(),
                tags: {}
            },
            {
                serviceName: 'LoadTestService',
                metricName: 'http.response_time_ms',
                value: randomIntBetween(20, 200),
                timestamp: new Date().toISOString(),
                tags: {}
            }
        ]
    });

    const headers = {
        'Content-Type': 'application/json',
        'X-API-KEY': API_KEY,
    };

    const res = http.post(`${API_URL}/api/metrics/ingest`, payload, { headers });

    check(res, {
        'status is 200': (r) => r.status === 200,
        'received field present': (r) => r.json('received') !== undefined,
    });
}