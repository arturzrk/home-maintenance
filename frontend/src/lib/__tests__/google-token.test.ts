import { readJwtExp, refreshGoogleIdToken } from "@/lib/google-token";

function makeJwt(payload: Record<string, unknown>): string {
  const header = Buffer.from(JSON.stringify({ alg: "none" })).toString("base64url");
  const body = Buffer.from(JSON.stringify(payload)).toString("base64url");
  return `${header}.${body}.signature`;
}

describe("readJwtExp", () => {
  it("returns the exp claim when present", () => {
    expect(readJwtExp(makeJwt({ exp: 1700000000 }))).toBe(1700000000);
  });

  it("returns undefined for malformed tokens", () => {
    expect(readJwtExp("not.a.jwt")).toBeUndefined();
    expect(readJwtExp("garbage")).toBeUndefined();
  });

  it("returns undefined when exp is missing or non-numeric", () => {
    expect(readJwtExp(makeJwt({}))).toBeUndefined();
    expect(readJwtExp(makeJwt({ exp: "later" }))).toBeUndefined();
  });
});

describe("refreshGoogleIdToken", () => {
  const baseArgs = {
    clientId: "client",
    clientSecret: "secret",
    refreshToken: "rt",
  };

  it("returns the new id_token and expiry from the JWT claim", async () => {
    const futureExp = Math.floor(Date.now() / 1000) + 3600;
    const fetchImpl = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        id_token: makeJwt({ exp: futureExp }),
        access_token: "new-access",
        expires_in: 3600,
      }),
    } as unknown as Response);

    const result = await refreshGoogleIdToken({ ...baseArgs, fetchImpl });

    expect(result.idToken).toContain(".");
    expect(result.accessToken).toBe("new-access");
    expect(result.expiresAt).toBe(futureExp);
    // Refresh token NOT rotated -> not overwritten.
    expect(result.refreshToken).toBeUndefined();
  });

  it("falls back to expires_in when the JWT carries no exp", async () => {
    const fetchImpl = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        id_token: makeJwt({}),
        expires_in: 3600,
      }),
    } as unknown as Response);

    const before = Math.floor(Date.now() / 1000);
    const result = await refreshGoogleIdToken({ ...baseArgs, fetchImpl });

    expect(result.expiresAt).toBeGreaterThanOrEqual(before + 3590);
    expect(result.expiresAt).toBeLessThanOrEqual(before + 3610);
  });

  it("surfaces a rotated refresh_token when Google returns one", async () => {
    const fetchImpl = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        id_token: makeJwt({ exp: 9_999_999_999 }),
        refresh_token: "rotated",
      }),
    } as unknown as Response);

    const result = await refreshGoogleIdToken({ ...baseArgs, fetchImpl });

    expect(result.refreshToken).toBe("rotated");
  });

  it("throws the Google error_description on a non-2xx response", async () => {
    const fetchImpl = jest.fn().mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({
        error: "invalid_grant",
        error_description: "Token has been expired or revoked.",
      }),
    } as unknown as Response);

    await expect(refreshGoogleIdToken({ ...baseArgs, fetchImpl })).rejects.toThrow(
      /Token has been expired or revoked/,
    );
  });

  it("throws when the payload has no id_token", async () => {
    const fetchImpl = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ access_token: "x", expires_in: 3600 }),
    } as unknown as Response);

    await expect(refreshGoogleIdToken({ ...baseArgs, fetchImpl })).rejects.toThrow(
      /no id_token/,
    );
  });

  it("posts to Google's token endpoint with the refresh grant", async () => {
    const fetchImpl = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        id_token: makeJwt({ exp: 9_999_999_999 }),
      }),
    } as unknown as Response);

    await refreshGoogleIdToken({ ...baseArgs, fetchImpl });

    const [url, init] = fetchImpl.mock.calls[0];
    expect(url).toBe("https://oauth2.googleapis.com/token");
    expect(init.method).toBe("POST");
    expect(init.headers["Content-Type"]).toBe("application/x-www-form-urlencoded");
    const body = init.body.toString();
    expect(body).toContain("grant_type=refresh_token");
    expect(body).toContain("client_id=client");
    expect(body).toContain("refresh_token=rt");
  });
});
