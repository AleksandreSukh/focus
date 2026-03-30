import { mapAuthFailure } from './errors.js';
import { GitHubAdapter } from '../gitProvider/adapters/githubAdapter.js';

export async function validateToken(token, probeOptions) {
  try {
    const adapter = new GitHubAdapter({
      owner: probeOptions.repoOwner,
      repo: probeOptions.repoName,
      branch: probeOptions.repoBranch,
      token,
    });
    await adapter.probeRepository('validating repository access');
    await adapter.probeBranch(
      probeOptions.repoBranch,
      `validating branch "${probeOptions.repoBranch}"`,
    );
    return { ok: true };
  } catch (error) {
    return {
      ok: false,
      error: mapAuthFailure(
        error,
        error?.contextLabel || 'validating repository access',
      ),
    };
  }
}
