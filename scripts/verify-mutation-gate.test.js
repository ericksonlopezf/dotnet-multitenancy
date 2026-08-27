// Copyright © Erickson Lopez. MIT License.
const assert = require('assert');
const {
  loadThresholds,
  parseScoreFromDescription,
  evaluateScore,
  verifyMutationGate,
  MAX_REPORT_AGE_DAYS
} = require('./verify-mutation-gate');

console.log('Running tests for verify-mutation-gate.js...\n');

// Test 1: loadThresholds from stryker-config.json
{
  const thresholds = loadThresholds();
  assert.strictEqual(thresholds.high, 100, 'Threshold high should be 100');
  assert.strictEqual(thresholds.low, 98, 'Threshold low should be 98');
  assert.strictEqual(thresholds.break, 95, 'Threshold break should be 95');
  console.log('✅ Test 1 Passed: loadThresholds loads correct values from stryker-config.json');
}

// Test 2: parseScoreFromDescription
{
  assert.strictEqual(parseScoreFromDescription('Stryker: 100% (240/240 killed) - ✅ HIGH'), 100);
  assert.strictEqual(parseScoreFromDescription('Stryker: 98.5% (200/203 killed) - 🟡 LOW'), 98.5);
  assert.strictEqual(parseScoreFromDescription('Stryker: 95.0% - 🟠 WARNING'), 95.0);
  assert.strictEqual(parseScoreFromDescription('Stryker: 94.2% - ❌ FAILED'), 94.2);
  assert.strictEqual(parseScoreFromDescription(null), null);
  assert.strictEqual(parseScoreFromDescription('No percentage here'), null);
  console.log('✅ Test 2 Passed: parseScoreFromDescription correctly extracts numeric percentage');
}

// Test 3: evaluateScore
{
  const thresholds = { high: 100, low: 98, break: 95 };

  const resHigh = evaluateScore(100, thresholds);
  assert.strictEqual(resHigh.status, '✅ HIGH');
  assert.strictEqual(resHigh.passedBreak, true);

  const resLow = evaluateScore(98.5, thresholds);
  assert.strictEqual(resLow.status, '🟡 LOW');
  assert.strictEqual(resLow.passedBreak, true);

  const resWarn = evaluateScore(96.0, thresholds);
  assert.strictEqual(resWarn.status, '🟠 WARNING');
  assert.strictEqual(resWarn.passedBreak, true);

  const resBreakExact = evaluateScore(95.0, thresholds);
  assert.strictEqual(resBreakExact.status, '🟠 WARNING');
  assert.strictEqual(resBreakExact.passedBreak, true);

  const resFail = evaluateScore(94.9, thresholds);
  assert.strictEqual(resFail.status, '❌ FAILED');
  assert.strictEqual(resFail.passedBreak, false);

  console.log('✅ Test 3 Passed: evaluateScore correctly categorizes scores and break gate');
}

// Test 4: verifyMutationGate with mock direct target SHA
(async () => {
  let failed = false;
  const mockContext = {
    repo: { owner: 'ericksonlopezf', repo: 'dotnet-multitenancy' },
    sha: 'abc1234567890'
  };

  const freshDate = new Date().toISOString();

  const mockGithub = {
    rest: {
      repos: {
        getCombinedStatusForRef: async ({ ref }) => {
          if (ref === 'abc1234567890') {
            return {
              data: {
                statuses: [
                  {
                    context: 'mutation-testing/stryker',
                    state: 'success',
                    description: 'Stryker: 100% (240/240 killed) - ✅ HIGH',
                    updated_at: freshDate,
                    target_url: 'https://github.com/ericksonlopezf/dotnet-multitenancy/actions/runs/12345'
                  }
                ]
              }
            };
          }
          return { data: { statuses: [] } };
        }
      }
    }
  };

  const mockCore = {
    setFailed: () => { failed = true; }
  };

  await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
  assert.strictEqual(failed, false, 'Should pass for 100% score on target commit');
  console.log('✅ Test 4 Passed: verifyMutationGate succeeds with direct 100% commit status');
})();

// Test 5: verifyMutationGate with score below break threshold
(async () => {
  let failed = false;
  const mockContext = {
    repo: { owner: 'ericksonlopezf', repo: 'dotnet-multitenancy' },
    sha: 'fail1234567890'
  };

  const freshDate = new Date().toISOString();

  const mockGithub = {
    rest: {
      repos: {
        getCombinedStatusForRef: async () => {
          return {
            data: {
              statuses: [
                {
                  context: 'mutation-testing/stryker',
                  state: 'failure',
                  description: 'Stryker: 80.0% (160/200 killed) - ❌ FAILED',
                  updated_at: freshDate,
                  target_url: 'https://github.com/ericksonlopezf/dotnet-multitenancy/actions/runs/12346'
                }
              ]
            }
          };
        }
      }
    }
  };

  const mockCore = {
    setFailed: () => { failed = true; }
  };

  try {
    await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
    assert.fail('Should have thrown an error for score below break threshold');
  } catch (err) {
    assert.strictEqual(failed, true, 'core.setFailed should be called');
    console.log('✅ Test 5 Passed: verifyMutationGate blocks release for sub-break score');
  }
})();
