// codex-review.js
import 'dotenv/config';
import OpenAI from "openai";
import simpleGit from "simple-git";
import chokidar from "chokidar";

const client = new OpenAI({ apiKey: process.env.OPENAI_API_KEY });
const git = simpleGit();

console.log("🚀 실시간 Codex 코드리뷰 시작...");
console.log("📁 파일 변경 감지 대기 중...");

chokidar.watch('.', { ignored: /node_modules|\.git/ }).on('change', async (path) => {
  try {
    const diff = await git.diff(['--cached']);
    if (!diff) return;
    console.log(`\n🔍 변경 감지: ${path}`);
    const prompt = `
다음은 코드 변경사항(diff)입니다.
코드 품질, 보안, 성능, 구조 개선점을 요약 리뷰해주세요.
-----------------------------
${diff}
`;

    const res = await client.chat.completions.create({
      model: "gpt-4o-mini",
      messages: [{ role: "user", content: prompt }],
    });

    console.log("\n💡 리뷰 결과:\n", res.choices[0].message.content);
  } catch (err) {
    console.error("에러 발생:", err);
  }
});
