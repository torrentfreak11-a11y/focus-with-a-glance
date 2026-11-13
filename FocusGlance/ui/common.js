'use strict';
/* Shared by the widget (index.html) and the settings window (settings.html). */

const DEFAULTS = {
  name: '', lang: 'en',
  use24h: false, showSeconds: true, startWithWindows: true, pinned: false,
  tint: 42, accent: '#8fdcff', glass: '#0c0e16', monitor: '', position: 'center',
  cards: { hijri: false, prayer: true, fx: true, weather: true, world: false, events: true, focus: true, meet: true, media: true, match: true },
  location: { mode: 'auto', city: '', lat: null, lon: null, cc: '' },
  prayer: { method: 'auto', school: 0, remindMin: 10, tune: { Fajr: 0, Sunrise: 0, Dhuhr: 0, Asr: 0, Maghrib: 0, Isha: 0 } },
  adhan: {
    enabled: true, sound: 'chime', file: '', fileLabel: '', volume: 80,
    prayers: { Fajr: true, Dhuhr: true, Asr: true, Maghrib: true, Isha: true },
    iqama: { Fajr: 25, Dhuhr: 20, Asr: 20, Maghrib: 10, Isha: 20 },
  },
  ramadan: 'auto',
  fx: { base: 'USD', quote: 'SAR' },
  hijri: { calendar: 'islamic-umalqura', lang: 'auto', digits: 'latn', style: 'long', adjust: 0 },
  focus: { work: 25, brk: 5, longBrk: 15, every: 4, sound: true },
  world: [{ tz: 'Europe/London', label: '' }, { tz: 'America/New_York', label: '' }],
  events: [], islamicEvents: true,
  icalUrls: [],
  // v2.2
  calendar: { primary: 'gregory', extra: 'islamic' },   // extra: none | islamic | chinese | gregory | coptic
  compactWeather: true, autoHide: true, btOverlay: true, welcomeCard: true, screenTime: true, hourChime: false, weatherAlerts: true,
  theme: 'glass', glyphs: true, themeSounds: true, avatar: 'pharaoh',        // theme: glass | pharaoh (Pharaonic pack)
  // live football (ESPN; GitHub build only): followed competitions, favourite teams, one followed game
  sports: { enabled: true, leagues: ['fifa.worldq.caf', 'caf.nations_qual', 'caf.nations', 'uefa.champions', 'eng.1', 'esp.1', 'ksa.1'], teams: [], onlyFavs: false, follow: '' },
};
const CAL_LOCKED = ['coptic'];   // calendars that need the Pharaonic pack
// ESPN competition codes the app offers (all checked against the live feed)
const LEAGUES = [
  ['fifa.world', 'FIFA World Cup'], ['fifa.worldq.caf', 'World Cup qualifying · Africa'], ['fifa.worldq.afc', 'World Cup qualifying · Asia'],
  ['fifa.worldq.uefa', 'World Cup qualifying · Europe'], ['caf.nations', 'Africa Cup of Nations'], ['caf.nations_qual', 'AFCON qualifying'],
  ['caf.champions', 'CAF Champions League'], ['afc.asian.cup', 'AFC Asian Cup'], ['uefa.champions', 'UEFA Champions League'],
  ['uefa.europa', 'UEFA Europa League'], ['eng.1', 'Premier League'], ['esp.1', 'LaLiga'], ['ita.1', 'Serie A'], ['ger.1', 'Bundesliga'],
  ['fra.1', 'Ligue 1'], ['ksa.1', 'Saudi Pro League'], ['usa.1', 'MLS'], ['fifa.friendly', 'International friendlies'],
];

function merge(def, val) {
  if (Array.isArray(def)) return Array.isArray(val) ? val : JSON.parse(JSON.stringify(def));
  if (def && typeof def === 'object') {
    const o = {};
    for (const k in def) o[k] = merge(def[k], val ? val[k] : undefined);
    if (val && typeof val === 'object') for (const k in val) if (!(k in def)) o[k] = val[k];
    return o;
  }
  return val === undefined || val === null ? def : val;
}
const getPath = (o, p) => p.split('.').reduce((a, k) => (a == null ? a : a[k]), o);
function setPath(o, p, v) { const ks = p.split('.'), last = ks.pop(); let c = o; for (const k of ks) c = c[k] ??= {}; c[last] = v; }

const pad = n => String(n).padStart(2, '0');
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

/* ------------------------------------------------------------------ i18n */
let LANG = 'en';
const T = {
  en: {
    hello: 'Hello', waveBack: 'Wave back', welcome: 'Welcome aboard', askName: 'What should I call you?',
    namePh: 'Your name', go: 'Continue', am: 'AM', pm: 'PM',
    Fajr: 'Fajr', Sunrise: 'Shuruq', Dhuhr: 'Dhuhr', Asr: 'Asr', Maghrib: 'Maghrib', Isha: 'Isha',
    prayer: 'Prayer', prayerTimes: 'Prayer times', in: 'in', loading: 'Loading', iqamaIn: 'Iqama in', iqama: 'Iqama',
    adhanNow: "It's time for", stop: 'Stop',
    rate: 'Rate', exchange: 'Exchange rate', weather: 'Weather', localTime: 'Local time',
    feels: 'Feels', humid: 'Humid', wind: 'Wind', kmh: 'km/h', hi: 'H', lo: 'L',
    wx0: 'Clear', wx1: 'Partly cloudy', wx2: 'Overcast', wx3: 'Fog / haze', wx4: 'Drizzle', wx5: 'Rain', wx6: 'Snow', wx7: 'Showers', wx8: 'Snow showers', wx9: 'Thunderstorm',
    left: 'left', meetIn: 'Meet in', noMeet: 'No Meet calls', meet: 'Google Meet', addIcal: 'Add your Google Calendar iCal links in Settings to get meeting countdowns.',
    inMeeting: 'In meeting', nextMeet: 'Next Meet', at: 'at', today: 'today', noMoreCalls: 'No more calls today', linksFailed: 'calendar link(s) failed',
    noSynced: 'No synced lyrics for this song', lookingLyrics: 'Looking for lyrics…', noLyrics: 'No lyrics found', pin: 'Pin lyrics', unpin: 'Unpin lyrics',
    settings: 'Settings',
    ramadan: 'Ramadan', ramadanKareem: 'Ramadan Kareem', day: 'Day', iftarIn: 'Iftar in', suhoorIn: 'Suhoor ends in', iftarAt: 'Iftar at', suhoorAt: 'Suhoor ends at', imsak: 'Imsak',
    focus: 'Focus', brk: 'Break', longBrk: 'Long break', sessions: 'sessions', ready: 'Ready', paused: 'Paused',
    timeToFocus: 'Break is over — ready to focus?', takeBreak: 'Great work! Take a break', focusDone: 'Focus session done',
    world: 'World clock', events: 'Countdown', noEvents: 'Add dates in Settings', daysN: 'days', dayOne: 'day', todayCap: 'Today', tomorrow: 'Tomorrow', yesterday: 'Yesterday',
    hijri: 'Hijri date', newYear: 'Islamic New Year', ramadanStart: 'Ramadan', eidFitr: 'Eid al-Fitr', arafah: 'Day of Arafah', eidAdha: 'Eid al-Adha',
    eventNow: 'Now', dShort: 'd', hShort: 'h',
    chooseLang: 'Choose your language', addCal: 'Add a second calendar?', showPrayer: 'Show prayer times and adhan alerts?',
    yes: 'Yes', no: 'No thanks', calNone: 'None', calislamic: 'Hijri', calchinese: 'Chinese', calgregory: 'Gregorian', calcoptic: 'Coptic',
    calTitle_islamic: 'Hijri date', calTitle_chinese: 'Chinese calendar', calTitle_gregory: 'Gregorian date', calTitle_coptic: 'Coptic calendar',
    btOn: 'Connected', btOff: 'Disconnected', battery: 'Battery', lyrics: 'Lyrics', gMorning: 'Good morning', gAfternoon: 'Good afternoon', gEvening: 'Good evening', gNight: 'Good night', screenTime: 'Screen time', mShort: 'min', rainAround: 'Rain around', snowAround: 'Snow around', stormAround: 'Storms around', wxWarn_storm: 'Thunderstorm expected', wxWarn_rain: 'Heavy rain expected', wxWarn_snow: 'Heavy snow expected', wxWarn_heat: 'Extreme heat', wxWarn_cold: 'Extreme cold', wxWarn_wind: 'Strong wind gusts', vs: 'vs', ft: 'FT', goal: 'GOAL!', openGate: 'Open the temple gate', kickoff: 'Kick-off', follow: 'Follow this game', unfollow: 'Unfollow', openMatch: 'Open match page',
  },
  zh: {
    hello: '你好', waveBack: '挥手回应', welcome: '欢迎使用', askName: '我该怎么称呼你？',
    namePh: '你的名字', go: '继续', am: '上午', pm: '下午',
    Fajr: '晨礼', Sunrise: '日出', Dhuhr: '晌礼', Asr: '晡礼', Maghrib: '昏礼', Isha: '宵礼',
    prayer: '礼拜', prayerTimes: '礼拜时间', in: '还有', loading: '加载中', iqamaIn: '距宣礼', iqama: '宣礼',
    adhanNow: '现在是', stop: '停止',
    rate: '汇率', exchange: '汇率', weather: '天气', localTime: '本地时间',
    feels: '体感', humid: '湿度', wind: '风速', kmh: '公里/时', hi: '高', lo: '低',
    wx0: '晴', wx1: '多云', wx2: '阴', wx3: '雾', wx4: '毛毛雨', wx5: '雨', wx6: '雪', wx7: '阵雨', wx8: '阵雪', wx9: '雷暴',
    left: '剩余', meetIn: '会议还有', noMeet: '没有会议', meet: 'Google Meet', addIcal: '在设置中添加 Google 日历的 iCal 链接，即可显示会议倒计时。',
    inMeeting: '会议中', nextMeet: '下一个会议', at: '于', today: '今天', noMoreCalls: '今天没有其他会议了', linksFailed: '个日历链接失败',
    noSynced: '这首歌没有同步歌词', lookingLyrics: '正在查找歌词…', noLyrics: '未找到歌词', pin: '固定歌词', unpin: '取消固定',
    settings: '设置',
    ramadan: '斋月', ramadanKareem: '斋月吉庆', day: '第', iftarIn: '开斋还有', suhoorIn: '封斋饭结束还有', iftarAt: '开斋时间', suhoorAt: '封斋饭截止', imsak: '封斋',
    focus: '专注', brk: '休息', longBrk: '长休息', sessions: '轮', ready: '就绪', paused: '已暂停',
    timeToFocus: '休息结束——准备专注了吗？', takeBreak: '干得好！休息一下吧', focusDone: '专注时段结束',
    world: '世界时钟', events: '倒计时', noEvents: '在设置中添加日期', daysN: '天', dayOne: '天', todayCap: '今天', tomorrow: '明天', yesterday: '昨天',
    hijri: '伊斯兰历', newYear: '伊斯兰新年', ramadanStart: '斋月', eidFitr: '开斋节', arafah: '阿拉法特日', eidAdha: '古尔邦节',
    eventNow: '现在', dShort: '天', hShort: '小时',
    chooseLang: '选择你的语言', addCal: '添加第二种日历？', showPrayer: '显示礼拜时间和提醒？',
    yes: '好的', no: '不用了', calNone: '不添加', calislamic: '伊斯兰历', calchinese: '农历', calgregory: '公历', calcoptic: '科普特历',
    calTitle_islamic: '伊斯兰历', calTitle_chinese: '农历', calTitle_gregory: '公历', calTitle_coptic: '科普特历',
    btOn: '已连接', btOff: '已断开', battery: '电量', lyrics: '歌词', gMorning: '早上好', gAfternoon: '下午好', gEvening: '晚上好', gNight: '夜深了', screenTime: '屏幕使用时间', mShort: '分钟', rainAround: '降雨预计于', snowAround: '降雪预计于', stormAround: '雷暴预计于', wxWarn_storm: '预计有雷暴', wxWarn_rain: '预计有大雨', wxWarn_snow: '预计有大雪', wxWarn_heat: '极端高温', wxWarn_cold: '极端低温', wxWarn_wind: '强阵风', vs: '对', ft: '完场', goal: '进球！', openGate: '打开神庙之门', kickoff: '开球', follow: '关注这场比赛', unfollow: '取消关注', openMatch: '打开比赛页面',
  },
  ar: {
    hello: 'أهلاً', waveBack: 'لوّح', welcome: 'مرحباً بك في Focus with a glance', askName: 'بماذا أناديك؟',
    namePh: 'اسمك', go: 'متابعة', am: 'ص', pm: 'م',
    Fajr: 'الفجر', Sunrise: 'الشروق', Dhuhr: 'الظهر', Asr: 'العصر', Maghrib: 'المغرب', Isha: 'العشاء',
    prayer: 'الصلاة', prayerTimes: 'أوقات الصلاة', in: 'بعد', loading: 'جارٍ التحميل', iqamaIn: 'الإقامة بعد', iqama: 'الإقامة',
    adhanNow: 'حان الآن وقت', stop: 'إيقاف',
    rate: 'السعر', exchange: 'سعر الصرف', weather: 'الطقس', localTime: 'الوقت المحلي',
    feels: 'تُحس', humid: 'رطوبة', wind: 'رياح', kmh: 'كم/س', hi: 'العظمى', lo: 'الصغرى',
    wx0: 'صافٍ', wx1: 'غائم جزئياً', wx2: 'غائم', wx3: 'ضباب', wx4: 'رذاذ', wx5: 'مطر', wx6: 'ثلج', wx7: 'زخات مطر', wx8: 'زخات ثلج', wx9: 'عاصفة رعدية',
    left: 'متبقٍ', meetIn: 'الاجتماع بعد', noMeet: 'لا اجتماعات', meet: 'Google Meet', addIcal: 'أضف روابط iCal من تقويم Google في الإعدادات لعرض العد التنازلي للاجتماعات.',
    inMeeting: 'في اجتماع', nextMeet: 'الاجتماع التالي', at: 'الساعة', today: 'اليوم', noMoreCalls: 'لا اجتماعات أخرى اليوم', linksFailed: 'رابط تقويم فشل',
    noSynced: 'لا توجد كلمات متزامنة لهذه الأغنية', lookingLyrics: 'جارٍ البحث عن الكلمات…', noLyrics: 'لم يتم العثور على كلمات', pin: 'تثبيت الكلمات', unpin: 'إلغاء التثبيت',
    settings: 'الإعدادات',
    ramadan: 'رمضان', ramadanKareem: 'رمضان كريم', day: 'اليوم', iftarIn: 'الإفطار بعد', suhoorIn: 'ينتهي السحور بعد', iftarAt: 'الإفطار الساعة', suhoorAt: 'ينتهي السحور الساعة', imsak: 'الإمساك',
    focus: 'تركيز', brk: 'استراحة', longBrk: 'استراحة طويلة', sessions: 'جلسات', ready: 'جاهز', paused: 'متوقف',
    timeToFocus: 'انتهت الاستراحة — جاهز للتركيز؟', takeBreak: 'أحسنت! خذ استراحة', focusDone: 'انتهت جلسة التركيز',
    world: 'الساعة العالمية', events: 'العد التنازلي', noEvents: 'أضف مواعيد من الإعدادات', daysN: 'يوماً', dayOne: 'يوم', todayCap: 'اليوم', tomorrow: 'غداً', yesterday: 'أمس',
    hijri: 'التاريخ الهجري', newYear: 'رأس السنة الهجرية', ramadanStart: 'رمضان', eidFitr: 'عيد الفطر', arafah: 'يوم عرفة', eidAdha: 'عيد الأضحى',
    eventNow: 'الآن', dShort: ' يوم', hShort: 'س',
    chooseLang: 'اختر لغتك', addCal: 'إضافة تقويم ثانٍ؟', showPrayer: 'عرض أوقات الصلاة وتنبيهات الأذان؟',
    yes: 'نعم', no: 'لا، شكراً', calNone: 'بدون', calislamic: 'الهجري', calchinese: 'الصيني', calgregory: 'الميلادي', calcoptic: 'القبطي',
    calTitle_islamic: 'التاريخ الهجري', calTitle_chinese: 'التقويم الصيني', calTitle_gregory: 'التاريخ الميلادي', calTitle_coptic: 'التقويم القبطي',
    btOn: 'متصل', btOff: 'غير متصل', battery: 'البطارية', lyrics: 'الكلمات', gMorning: 'صباح الخير', gAfternoon: 'مساء الخير', gEvening: 'مساء الخير', gNight: 'تصبح على خير', screenTime: 'وقت الشاشة', mShort: 'د', rainAround: 'أمطار حوالي', snowAround: 'ثلوج حوالي', stormAround: 'عواصف حوالي', wxWarn_storm: 'عاصفة رعدية متوقعة', wxWarn_rain: 'أمطار غزيرة متوقعة', wxWarn_snow: 'ثلوج كثيفة متوقعة', wxWarn_heat: 'حرارة شديدة', wxWarn_cold: 'برد شديد', wxWarn_wind: 'رياح قوية', vs: 'ضد', ft: 'انتهت', goal: 'هدف!', openGate: 'افتح بوابة المعبد', kickoff: 'بداية المباراة', follow: 'متابعة هذه المباراة', unfollow: 'إلغاء المتابعة', openMatch: 'فتح صفحة المباراة',
  },
};
const t = k => (T[LANG] && T[LANG][k]) ?? T.en[k] ?? k;
function daysText(n) {
  if (isZh()) return n + ' 天';
  if (!isAr()) return n + ' ' + (n === 1 ? 'day' : 'days');
  if (n === 1) return 'يوم واحد';
  if (n === 2) return 'يومان';
  return n + ' ' + (n <= 10 ? 'أيام' : 'يوماً');
}
const isAr = () => LANG === 'ar';
const isZh = () => LANG === 'zh';
const gregLocale = () => (isAr() ? 'ar-u-ca-gregory-nu-latn' : isZh() ? 'zh-CN' : 'en-GB');
const LANGS = [['en', 'English'], ['ar', 'العربية'], ['zh', '中文']];

/* ------------------------------------------------------------------ calendars */
// One line of text for a date in any supported calendar. style: long | weekday | short
function calText(d, cal, c, style) {
  if (cal === 'islamic') return hijriText(d, c.hijri, style);
  const st = style || 'long';
  const o = st === 'weekday' ? { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }
    : st === 'short' ? { day: 'numeric', month: 'long' } : { day: 'numeric', month: 'long', year: 'numeric' };
  if (cal === 'gregory') { const f = hijriFormatter(gregLocale(), o); return f ? f.format(d) : d.toDateString(); }
  if (cal === 'chinese') {
    // the Chinese lunar calendar reads naturally in Chinese, whatever the UI language: 丙午年十月初三
    const f = hijriFormatter('zh-CN-u-ca-chinese', { year: 'numeric', month: 'long', day: 'numeric' });
    if (!f) return '';
    const p = f.formatToParts(d), g = ty => (p.find(q => q.type === ty) || {}).value || '';
    const md = g('month') + lunarDay(+g('day'));
    if (st === 'short') return md;
    const wd = st === 'weekday' ? ' ' + hijriFormatter('zh-CN', { weekday: 'long' }).format(d) : '';
    return (g('yearName') ? g('yearName') + '年' : '') + md + wd;
  }
  if (cal === 'coptic') {
    const f = hijriFormatter((isAr() ? 'ar' : 'en') + '-u-ca-coptic-nu-latn', { day: 'numeric', month: 'long', year: 'numeric' });
    if (!f) return '';
    const p = f.formatToParts(d), g = ty => (p.find(q => q.type === ty) || {}).value || '';
    const wd = st === 'weekday' ? hijriFormatter(gregLocale(), { weekday: 'long' }).format(d) + ', ' : '';
    return st === 'short' ? `${g('day')} ${g('month')}` : `${wd}${g('day')} ${g('month')} ${g('year')} ${isAr() ? 'ش' : 'A.M.'}`;
  }
  return '';
}

// 1 → 初一, 15 → 十五, 21 → 廿一, 30 → 三十
function lunarDay(n) {
  const D = '一二三四五六七八九十';
  if (!(n >= 1 && n <= 30)) return String(n);
  if (n <= 10) return '初' + D[n - 1];
  if (n < 20) return '十' + D[n - 11];
  if (n === 20) return '二十';
  if (n < 30) return '廿' + D[n - 21];
  return '三十';
}

/* ------------------------------------------------------------------ hieroglyphs (decorative) */
// One uniliteral sign per sound: enough to write a name inside a cartouche.
const GLYPH = {
  a: '𓄿', b: '𓃀', c: '𓎡', d: '𓂧', e: '𓇋', f: '𓆑', g: '𓎼', h: '𓉔', i: '𓇋', j: '𓆓', k: '𓎡', l: '𓃭', m: '𓅓', n: '𓈖',
  o: '𓍯', p: '𓊪', q: '𓈎', r: '𓂋', s: '𓋴', t: '𓏏', u: '𓅱', v: '𓆑', w: '𓅱', x: '𓎡𓋴', y: '𓇌', z: '𓊃',
  'ا': '𓄿', 'أ': '𓄿', 'إ': '𓄿', 'آ': '𓄿', 'ب': '𓃀', 'ت': '𓏏', 'ث': '𓍿', 'ج': '𓆓', 'ح': '𓎛', 'خ': '𓐍', 'د': '𓂧', 'ذ': '𓆓',
  'ر': '𓂋', 'ز': '𓊃', 'س': '𓋴', 'ش': '𓈙', 'ص': '𓋴', 'ض': '𓂧', 'ط': '𓏏', 'ظ': '𓊃', 'ع': '𓂝', 'غ': '𓎼', 'ف': '𓆑',
  'ق': '𓈎', 'ك': '𓎡', 'ل': '𓃭', 'م': '𓅓', 'ن': '𓈖', 'ه': '𓉔', 'ة': '𓏏', 'و': '𓅱', 'ي': '𓇌', 'ى': '𓇌', 'ء': '𓄿',
};
const toGlyphs = s => [...String(s || '').toLowerCase()].map(ch => GLYPH[ch] || '').join('');

/* ------------------------------------------------------------------ prayer */
const PRAYERS = ['Fajr', 'Sunrise', 'Dhuhr', 'Asr', 'Maghrib', 'Isha'];
const SALAH = ['Fajr', 'Dhuhr', 'Asr', 'Maghrib', 'Isha'];
const METHOD_BY_CC = { SA: 4, AE: 16, KW: 9, QA: 10, BH: 8, OM: 8, EG: 5, TR: 13, US: 2, CA: 2, SG: 11, MY: 17, ID: 20, FR: 12, RU: 14, JO: 23, DZ: 19, TN: 18, MA: 21, PT: 22 };
const METHODS = [
  [4, 'Umm Al-Qura, Makkah', 'أم القرى - مكة المكرمة'],
  [3, 'Muslim World League', 'رابطة العالم الإسلامي'],
  [5, 'Egyptian General Authority of Survey', 'الهيئة المصرية العامة للمساحة'],
  [2, 'ISNA (North America)', 'الجمعية الإسلامية لأمريكا الشمالية'],
  [1, 'University of Islamic Sciences, Karachi', 'جامعة العلوم الإسلامية بكراتشي'],
  [8, 'Gulf Region', 'منطقة الخليج'],
  [16, 'Dubai', 'دبي'],
  [9, 'Kuwait', 'الكويت'],
  [10, 'Qatar', 'قطر'],
  [23, 'Jordan (Ministry of Awqaf)', 'الأردن (وزارة الأوقاف)'],
  [19, 'Algeria', 'الجزائر'],
  [18, 'Tunisia', 'تونس'],
  [21, 'Morocco', 'المغرب'],
  [13, 'Diyanet, Türkiye', 'تركيا (ديانت)'],
  [12, 'UOIF, France', 'فرنسا (UOIF)'],
  [22, 'Lisbon (Comunidade Islâmica)', 'لشبونة'],
  [14, 'Muslims of Russia', 'روسيا'],
  [11, 'MUIS, Singapore', 'سنغافورة'],
  [17, 'JAKIM, Malaysia', 'ماليزيا (JAKIM)'],
  [20, 'KEMENAG, Indonesia', 'إندونيسيا'],
  [15, 'Moonsighting Committee Worldwide', 'لجنة رؤية الهلال العالمية'],
  [7, 'Institute of Geophysics, Tehran', 'معهد الجيوفيزياء - طهران'],
  [0, 'Jafari (Shia Ithna-Ashari)', 'الجعفري'],
];
function methodFor(cfg, cc) { return cfg.prayer.method === 'auto' ? (METHOD_BY_CC[cc] ?? 3) : +cfg.prayer.method; }

/* ------------------------------------------------------------------ Hijri */
const _hf = {};
function hijriFormatter(locale, opts) {
  const k = locale + JSON.stringify(opts);
  try { return _hf[k] ||= new Intl.DateTimeFormat(locale, opts); } catch { return null; }
}
function shiftDays(d, n) { const x = new Date(d); x.setDate(x.getDate() + (n || 0)); return x; }
function hijriParts(d, h) {
  const f = hijriFormatter('en-u-ca-' + h.calendar + '-nu-latn', { day: 'numeric', month: 'numeric', year: 'numeric' });
  if (!f) return null;
  const p = f.formatToParts(shiftDays(d, h.adjust));
  const g = ty => parseInt((p.find(q => q.type === ty) || {}).value, 10);
  return { d: g('day'), m: g('month'), y: g('year') };
}
function hijriText(d, h, style) {
  const lang = h.lang === 'auto' ? LANG : h.lang;
  const loc = (lang === 'ar' ? 'ar' : lang === 'zh' ? 'zh-CN' : 'en-GB') + '-u-ca-' + h.calendar + '-nu-' + (h.digits === 'arab' ? 'arab' : 'latn');
  const st = style || h.style;
  const opts = st === 'weekday' ? { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }
    : st === 'short' ? { day: 'numeric', month: 'long' }
    : st === 'numeric' ? { day: '2-digit', month: '2-digit', year: 'numeric' }
    : { day: 'numeric', month: 'long', year: 'numeric' };
  const f = hijriFormatter(loc, opts);
  return f ? f.format(shiftDays(d, h.adjust)) : '';
}

/* Islamic occasions in the next ~13 months, computed from the chosen Hijri calendar. */
const ISLAMIC = [[1, 1, 'newYear', '🌙'], [9, 1, 'ramadanStart', '🌙'], [10, 1, 'eidFitr', '🎉'], [12, 9, 'arafah', '🕋'], [12, 10, 'eidAdha', '🐑']];
const _ie = { key: '', list: [] };
function islamicOccasions(h) {
  const base = new Date(); base.setHours(0, 0, 0, 0);
  const key = base.toDateString() + JSON.stringify(h);
  if (_ie.key === key) return _ie.list;
  const list = [];
  for (let i = 0; i < 400 && list.length < ISLAMIC.length; i++) {
    const d = shiftDays(base, i), p = hijriParts(d, h); if (!p) break;
    const w = ISLAMIC.find(x => x[0] === p.m && x[1] === p.d);
    if (w && !list.some(e => e.k === w[2])) list.push({ k: w[2], at: +d, emoji: w[3], allDay: true, islamic: true });
  }
  _ie.key = key; _ie.list = list;
  return list;
}

/* ------------------------------------------------------------------ sound */
// All sounds are synthesized here (original compositions, no sound files).
//   adhan  — an airy "welcome": a warm pad swells while a soft bell arpeggio rises, then a high shimmer
//   focus  — three bright notes          hour — two soft notes          warn — three gentle falling tones
//   bell   — Pharaonic: bronze temple bell + sistrum shake (used for the Pharaonic theme's alerts)
function chime(vol = 0.7, kind = 'adhan') {
  try {
    const ac = chime.ac || (chime.ac = new AudioContext());
    if (ac.state === 'suspended') ac.resume();
    const out = ac.createGain(); out.gain.value = Math.max(0, Math.min(1, vol)) * 0.5;
    // a soft hall: generated impulse response (decaying stereo noise)
    if (!chime.ir) {
      const len = ac.sampleRate * 3.2, ir = ac.createBuffer(2, len, ac.sampleRate);
      for (let c = 0; c < 2; c++) { const d = ir.getChannelData(c); for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / len, 3.2); }
      chime.ir = ir;
    }
    const verb = ac.createConvolver(); verb.buffer = chime.ir;
    const wet = ac.createGain(); wet.gain.value = kind === 'adhan' || kind === 'bell' ? .55 : .3;
    out.connect(ac.destination); out.connect(verb); verb.connect(wet); wet.connect(ac.destination);
    const T = ac.currentTime + .05;
    const tone = (f, at, dur, g, partials = [[1, 1], [2.01, .3], [3.02, .08]], type = 'sine') => {
      for (const [mul, pg] of partials) {
        const o = ac.createOscillator(), e = ac.createGain();
        o.type = type; o.frequency.value = f * mul;
        e.gain.setValueAtTime(.0001, T + at); e.gain.exponentialRampToValueAtTime(g * pg, T + at + .015);
        e.gain.exponentialRampToValueAtTime(.0001, T + at + dur);
        o.connect(e); e.connect(out); o.start(T + at); o.stop(T + at + dur + .05);
      }
    };
    if (kind === 'adhan') {
      // warm pad: two slightly detuned saws through a slowly opening low-pass
      const lp = ac.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.setValueAtTime(400, T); lp.frequency.exponentialRampToValueAtTime(2600, T + 1.6); lp.frequency.exponentialRampToValueAtTime(900, T + 4.2);
      const pg = ac.createGain(); pg.gain.setValueAtTime(.0001, T); pg.gain.exponentialRampToValueAtTime(.16, T + 1.1); pg.gain.exponentialRampToValueAtTime(.0001, T + 4.6);
      lp.connect(pg); pg.connect(out);
      for (const [f, dt] of [[261.63, -4], [261.63, 5], [392, 0], [329.63, 3]]) { const o = ac.createOscillator(); o.type = 'sawtooth'; o.frequency.value = f; o.detune.value = dt; o.connect(lp); o.start(T); o.stop(T + 4.7); }
      // rising bell arpeggio (Cmaj9) and a last airy shimmer
      [[523.25, .25], [659.25, .48], [783.99, .71], [987.77, .94], [1174.66, 1.17]].forEach(([f, at]) => tone(f, at, 3.2, .42, [[1, 1], [2.0, .22], [3.99, .06]]));
      tone(2093, 1.45, 2.6, .12, [[1, 1], [1.5, .3]]);
    } else if (kind === 'bell') {
      // bronze temple bell (inharmonic partials) …
      tone(196, 0, 4.2, .5, [[1, 1], [2.0, .55], [2.76, .4], [4.07, .22], [5.4, .12]]);
      tone(392, .9, 3, .22, [[1, 1], [2.76, .35], [5.4, .1]]);
      // … and a sistrum: quick shakes of bright metallic noise
      for (let k = 0; k < 6; k++) {
        const len = ac.sampleRate * .09, b = ac.createBuffer(1, len, ac.sampleRate), d = b.getChannelData(0);
        for (let i = 0; i < len; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / len, 2);
        const s = ac.createBufferSource(); s.buffer = b;
        const hp = ac.createBiquadFilter(); hp.type = 'bandpass'; hp.frequency.value = 6500; hp.Q.value = 2.5;
        const g = ac.createGain(); g.gain.value = .22;
        s.connect(hp); hp.connect(g); g.connect(out); s.start(T + .15 + k * .13);
      }
    } else if (kind === 'hour') {
      tone(783.99, 0, 2.2, .35); tone(1046.5, .32, 2.6, .3);
    } else if (kind === 'warn') {
      tone(880, 0, .9, .38); tone(698.46, .28, .9, .36); tone(587.33, .56, 1.6, .36);
    } else {
      tone(659.25, 0, 2.4, .5); tone(987.77, .2, 2.4, .45); tone(1318.5, .4, 2.6, .4);
    }
  } catch { }
}
