import type { TranslationKey } from './en';

/**
 * The Arabic wording.
 *
 * Typed by the **keys** of `en`, so one added there and forgotten here will not compile.
 * The values are only strings: `en` is written `as const`, which makes each of its
 * values a type of its own, and asking Arabic to match those would be asking it to be
 * English.
 *
 * ### The words the factory has to confirm
 *
 * Four terms below are **the factory's own**, taken off a real bag label recorded in
 * specification section 12, and are not to be changed without them: `رقم الرول` (roll
 * number), `نوع الطبق` (product), `عدد الأطباق / كيس` (plates per bag) and `الوردية`
 * (shift). A system that calls a shift something other than what the floor calls it is
 * a system the floor does not trust.
 *
 * The rest of the plant vocabulary is a careful draft and nothing more. These in
 * particular should be read out to somebody on the floor before this reaches them:
 *
 * | Here | English | Worth asking |
 * |---|---|---|
 * | `المنصات` | pallets | `طبالي` is what many plants actually say |
 * | `الإكسترودر` | extruder | transliterated, because that is usually what is said |
 * | `التشكيل الحراري` | thermoforming | correct, but the floor may have a shorter word |
 * | `إعادة التدوير` | recycler | the machine, not the activity |
 * | `الوصفات` | recipes | |
 *
 * Getting these wrong is worse than leaving the screens in English: a man navigating by
 * the shape of a familiar English word is not lost, but a man reading a word that means
 * something else in his trade is.
 */
export const ar: Record<TranslationKey, string> = {
  'nav.main': 'الرئيسية',
  'nav.operations': 'العمليات',
  'nav.production': 'الإنتاج',
  'nav.analytics': 'التحليلات',
  'nav.management': 'الإدارة',

  'nav.dashboard': 'لوحة التحكم',
  'nav.inventory': 'المخزون',
  'nav.trace': 'تتبّع ملصق',
  'nav.receive': 'استلام المواد',
  'nav.issue': 'صرف المواد',
  'nav.rolls': 'إنتاج الرولات',
  'nav.rollTests': 'فحوصات الرولات',
  'nav.thermo': 'التشكيل الحراري',
  'nav.thermoTests': 'فحوصات التشكيل',
  'nav.pallets': 'المنصات',
  'nav.packaging': 'التغليف',
  'nav.dispatch': 'الشحن',
  'nav.recycler': 'إعادة التدوير',
  'nav.reports': 'التقارير',
  'nav.audit': 'سجل التدقيق',
  'nav.recipes': 'الوصفات',
  'nav.shifts': 'الورديات',
  'nav.masterData': 'البيانات الأساسية',
  'nav.users': 'المستخدمون',
  'nav.collapse': 'طي القائمة',

  // The name stays in Latin letters: it is what is painted on the building.
  'app.name': 'Colors ERP',
  'app.tagline': 'مصنع الستايروفوم',
  'top.openMenu': 'فتح القائمة',
  'top.signOut': 'تسجيل الخروج',
  // The button always shows the language it switches *to*, so it reads as an offer
  // rather than a statement of where you already are.
  'top.language': 'English',
  'top.languageLabel': 'التبديل إلى الإنجليزية',

  'page.dashboard.title': 'لوحة التحكم',
  'page.inventory.title': 'المخزون',
  'page.inventory.subtitle': 'ما يحتويه المخزن، بوحدة كل مادة.',
  'page.trace.title': 'من أين جاء هذا؟',
  'page.trace.subtitle': 'امسح رولاً أو كيساً أو منصة لعرض كل خطوة قبله.',
  'page.receive.title': 'استلام المواد',
  'page.receive.subtitle': 'تسجيل توريد إلى المخزن.',
  'page.issue.title': 'صرف المواد',
  'page.issue.subtitle': 'المواد المصروفة، والمرتجع، وما استُهلك فعلياً.',
  'page.rolls.title': 'إنتاج الرولات',
  'page.rolls.subtitle': 'الرولات الخارجة من الإكسترودر، لكل منها وصفته ولونه.',
  'page.rollTests.title': 'فحوصات الرولات',
  'page.rollTests.subtitle': 'الوزن والطول ووزن الطبق وأربع قراءات للسماكة.',
  'page.thermo.title': 'التشكيل الحراري',
  'page.thermo.subtitle': 'يدخل الرول كاملاً.',
  'page.thermoTests.title': 'فحوصات التشكيل الحراري',
  'page.thermoTests.subtitle': 'الأكياس ووزن القطعة ووزن الكيس، تُحصى بعد التشغيلة.',
  'page.pallets.title': 'المنصات',
  'page.pallets.subtitle': 'المنصات قيد التجهيز، والأكياس عليها.',
  'page.packaging.title': 'التغليف',
  'page.packaging.subtitle': 'ما استهلكه كل خط للتغليف.',
  'page.dispatch.title': 'الشحن',
  'page.dispatch.subtitle': 'المنصات الجاهزة الخارجة من المصنع.',
  'page.recycler.title': 'إعادة التدوير',
  'page.recycler.subtitle': 'كمية المادة المعاد تدويرها التي أنتجتها الوردية.',
  'page.reports.title': 'التقارير',
  'page.reports.subtitle': 'محسوبة مما سجّلته الورديات.',
  'page.audit.title': 'سجل التدقيق',
  'page.audit.subtitle': 'من غيّر ماذا، وما الذي رُفض.',
  'page.recipes.title': 'الوصفات',
  'page.recipes.subtitle': 'العائلات الأربع وكل إصدار.',
  'page.shifts.title': 'الورديات',
  'page.shifts.subtitle': 'سجل واحد لكل وردية. أغلقه عند انتهاء العمل.',
  'page.masterData.title': 'البيانات الأساسية',
  'page.users.title': 'المستخدمون',
  'page.users.subtitle': 'من يمكنه الدخول، وما يستطيع كل منهم فعله.',

  'common.save': 'حفظ',
  'common.cancel': 'إلغاء',
  'common.close': 'إغلاق',
  'common.saving': 'جارٍ الحفظ…',
  'common.loading': 'جارٍ التحميل…',
  'common.search': 'بحث',
  'common.somethingWentWrong': 'حدث خطأ. حاول مرة أخرى.',

  // Words used on screen after screen. See the note in ar.ts.
  'term.roll': 'الرول',
  'term.rolls': 'الرولات',
  'term.rollCode': 'رمز الرول',
  'term.bag': 'الكيس',
  'term.bags': 'الأكياس',
  'term.pallet': 'المنصة',
  'term.pallets': 'المنصات',
  'term.shift': 'الوردية',
  'term.recipe': 'الوصفة',
  'term.mould': 'القالب',
  'term.material': 'المادة',
  'term.materials': 'المواد',
  'term.product': 'المنتج',
  'term.colour': 'اللون',
  'term.line': 'الخط',
  'term.barcode': 'الباركود',
  'term.ticket': 'التذكرة',
  'term.crew': 'الطاقم',
  'term.supervisor': 'مشرف',
  'term.absorbent': 'ماص',
  'term.recycledMaterial': 'المادة المعاد تدويرها',
  'term.colourRecipe': 'اللون · الوصفة',
  'term.fromRoll': 'من الرول',
  'field.status': 'الحالة',
  'field.weight': 'الوزن',
  'field.weightKg': 'الوزن (كغ)',
  'field.length': 'الطول',
  'field.pieces': 'القطع',
  'field.minutes': 'الدقائق',
  'field.note': 'ملاحظة',
  'field.notes': 'ملاحظات',
  'field.name': 'الاسم',
  'field.code': 'الرمز',
  'field.category': 'الفئة',
  'field.baseUnit': 'الوحدة الأساسية',
  'field.packSizes': 'أحجام العبوات',
  'field.minimum': 'الحد الأدنى',
  'field.inStock': 'المتوفر',
  'field.employeeNumber': 'الرقم الوظيفي',
  'field.newPassword': 'كلمة المرور الجديدة',
  'field.whatHeMayDo': 'ما يستطيع فعله',
  'field.when': 'الوقت',
  'field.from': 'من',
  'field.goingTo': 'إلى',
  'field.out': 'الصادر',
  'field.used': 'المستهلك',
  'field.issued': 'المصروف',
  'field.returned': 'مرتجع',
  'field.difference': 'الفرق',
  'field.recordedBy': 'سجّلها',
  'field.made': 'أُنتج',
  'field.rollsMade': 'الرولات المنتَجة',
  'field.rollsFormed': 'الرولات المُشكَّلة',
  'field.lostInForming': 'الفاقد في التشكيل',
  'action.edit': 'تعديل',
  'action.delete': 'حذف',
  'action.copy': 'نسخ',
  'action.back': 'رجوع',
  'action.discard': 'تجاهل',
  'action.choose': 'اختر…',
  'action.openShift': 'فتح وردية',
  'action.issueMaterial': 'صرف مواد',
  'action.addWorker': 'إضافة عامل',
  'action.readWithCamera': 'قراءة الملصق بالكاميرا',
  'state.saved': 'تم الحفظ',
  'state.recorded': 'مسجّل',
  'state.finished': 'مكتمل',
  'state.notDecidedYet': 'لم يُحدَّد بعد',
  'state.everyRun': 'كل تشغيلة',
  'msg.reportFailed': 'تعذّر تحميل التقرير.',
  'msg.writtenOnceAtShiftEnd': 'يُكتب مرة واحدة، في نهاية الوردية.',
  'msg.alreadyRecordedForLine': 'مسجّل مسبقاً لهذا الخط.',
  'msg.noPackingLineOpen': 'لا يوجد خط تغليف مفتوح.',
};
