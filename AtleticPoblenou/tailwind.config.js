/** @type {import('tailwindcss').Config} */
// Los colores son tokens semánticos definidos como variables CSS en wwwroot/css/app.css.
// Cambian con el tema (clase `dark` en <html>) sin tocar los componentes.
const apnToken = (name) => `rgb(var(--c-${name}) / <alpha-value>)`;

module.exports = {
  darkMode: 'class',
  content: [
    './wwwroot/index.html',
    './App.razor',
    './Components/**/*.razor',
    './Layout/**/*.razor',
    './Pages/**/*.razor',
    './Services/**/*.cs',
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Barlow', 'Helvetica Neue', 'Helvetica', 'Arial', 'sans-serif'],
        cond: ['"Barlow Condensed"', '"Arial Narrow"', 'Helvetica Neue', 'Arial', 'sans-serif'],
      },
      colors: {
        app: apnToken('app'),
        surface: apnToken('surface'),
        well: apnToken('well'),
        well2: apnToken('well2'),
        line: apnToken('line'),
        line2: apnToken('line2'),
        ink: apnToken('ink'),
        ink2: apnToken('ink2'),
        muted: apnToken('muted'),
        faint: apnToken('faint'),
        club: apnToken('club'),
        clubdeep: apnToken('clubdeep'),
        clubtext: apnToken('clubtext'),
        clubbg: apnToken('clubbg'),
        ok: apnToken('ok'),
        okdeep: apnToken('okdeep'),
        oktext: apnToken('oktext'),
        okbg: apnToken('okbg'),
        warn: apnToken('warn'),
        warntext: apnToken('warntext'),
        warnbg: apnToken('warnbg'),
        info: apnToken('info'),
        infotext: apnToken('infotext'),
        infobg: apnToken('infobg'),
        hero: apnToken('hero'),
        herotext: apnToken('herotext'),
        heromuted: apnToken('heromuted'),
        heroline: apnToken('heroline'),
        heroline2: apnToken('heroline2'),
        herook: apnToken('herook'),
        heroclub: apnToken('heroclub'),
        poblenou: {
          red: '#E53935',
          darkRed: '#B71C1C',
          gold: '#F59E0B',
          green: '#15803D',
          blue: '#0284C7',
          card: '#18181B',
          dark: '#09090B',
        },
      },
      zIndex: {
        60: '60',
      },
    },
  },
  plugins: [],
};
