import{ar as le,as as ue,at as ce,au as me,av as de,aw as X,ax as pe,ay as d,az as F,aA as fe,aB as ge,aC as _,aD as be,aE as q,aF as he,aG as ye,aH as D,aI as ve,aJ as Ee,aK as z,aL as w,aM as H}from"./index-B7jvAjdE.js";import{t as Pe}from"./index-B_QkKjtG.js";function Ie(t,r){return(t%r+r)%r}function P(t){if(typeof window.devicePixelRatio!="number")return Math.round(t);const r=window.devicePixelRatio;return Math.floor(t*r+.5)/r}function N(t,r){return Math.min(Math.max(E(t),r.min),r.max)}function G(t){if(!Number.isFinite(t))return 0;let r=1,e=0;for(;Math.round(t*r)/r!==t;)r*=10,e+=1;return e}var Te=(t,r)=>J(E(t),"+",r),Ne=(t,r)=>J(E(t),"-",r);function E(t){return Number.isNaN(t)?0:t}function Re(t,r){return E(t)>=r.max}function Se(t,r){return E(t)<=r.min}function Ce(t,r){return E(t)>=r.min&&E(t)<=r.max}function J(t,r,e){let n=r==="+"?t+e:t-e;if(t%1!==0||e%1!==0){const i=10**Math.max(G(t),G(e));t=Math.round(t*i),e=Math.round(e*i),n=r==="+"?t+e:t-e,n/=i}return n}let A=new Map,V=!1;try{V=new Intl.NumberFormat("de-DE",{signDisplay:"exceptZero"}).resolvedOptions().signDisplay==="exceptZero"}catch{}let C=!1;try{C=new Intl.NumberFormat("de-DE",{style:"unit",unit:"degree"}).resolvedOptions().style==="unit"}catch{}const Q={degree:{narrow:{default:"°","ja-JP":" 度","zh-TW":"度","sl-SI":" °"}}};class De{format(r){let e="";if(!V&&this.options.signDisplay!=null?e=$e(this.numberFormatter,this.options.signDisplay,r):e=this.numberFormatter.format(r),this.options.style==="unit"&&!C){var n;let{unit:i,unitDisplay:s="short",locale:u}=this.resolvedOptions();if(!i)return e;let l=(n=Q[i])===null||n===void 0?void 0:n[s];e+=l[u]||l.default}return e}formatToParts(r){return this.numberFormatter.formatToParts(r)}formatRange(r,e){if(typeof this.numberFormatter.formatRange=="function")return this.numberFormatter.formatRange(r,e);if(e<r)throw new RangeError("End date must be >= start date");return`${this.format(r)} – ${this.format(e)}`}formatRangeToParts(r,e){if(typeof this.numberFormatter.formatRangeToParts=="function")return this.numberFormatter.formatRangeToParts(r,e);if(e<r)throw new RangeError("End date must be >= start date");let n=this.numberFormatter.formatToParts(r),i=this.numberFormatter.formatToParts(e);return[...n.map(s=>({...s,source:"startRange"})),{type:"literal",value:" – ",source:"shared"},...i.map(s=>({...s,source:"endRange"}))]}resolvedOptions(){let r=this.numberFormatter.resolvedOptions();return!V&&this.options.signDisplay!=null&&(r={...r,signDisplay:this.options.signDisplay}),!C&&this.options.style==="unit"&&(r={...r,style:"unit",unit:this.options.unit,unitDisplay:this.options.unitDisplay}),r}constructor(r,e={}){this.numberFormatter=Ae(r,e),this.options=e}}function Ae(t,r={}){let{numberingSystem:e}=r;if(e&&t.includes("-nu-")&&(t.includes("-u-")||(t+="-u-"),t+=`-nu-${e}`),r.style==="unit"&&!C){var n;let{unit:u,unitDisplay:l="short"}=r;if(!u)throw new Error('unit option must be provided with style: "unit"');if(!(!((n=Q[u])===null||n===void 0)&&n[l]))throw new Error(`Unsupported unit ${u} with unitDisplay = ${l}`);r={...r,style:"decimal"}}let i=t+(r?Object.entries(r).sort((u,l)=>u[0]<l[0]?-1:1).join():"");if(A.has(i))return A.get(i);let s=new Intl.NumberFormat(t,r);return A.set(i,s),s}function $e(t,r,e){if(r==="auto")return t.format(e);if(r==="never")return t.format(Math.abs(e));{let n=!1;if(r==="always"?n=e>0||Object.is(e,0):r==="exceptZero"&&(Object.is(e,-0)||Object.is(e,0)?e=Math.abs(e):n=e>0),n){let i=t.format(-e),s=t.format(e),u=i.replace(s,"").replace(/\u200e|\u061C/,"");return[...u].length!==1&&console.warn("@react-aria/i18n polyfill for NumberFormat signDisplay: Unsupported case"),i.replace(s,"!!!").replace(u,"+").replace("!!!",s)}else return t.format(e)}}const Oe=new RegExp("^.*\\(.*\\).*$"),we=["latn","arab","hanidec","deva","beng"];class ee{parse(r){return $(this.locale,this.options,r).parse(r)}isValidPartialNumber(r,e,n){return $(this.locale,this.options,r).isValidPartialNumber(r,e,n)}getNumberingSystem(r){return $(this.locale,this.options,r).options.numberingSystem}constructor(r,e={}){this.locale=r,this.options=e}}const B=new Map;function $(t,r,e){let n=k(t,r);if(!t.includes("-nu-")&&!n.isValidPartialNumber(e)){for(let i of we)if(i!==n.options.numberingSystem){let s=k(t+(t.includes("-u-")?"-nu-":"-u-nu-")+i,r);if(s.isValidPartialNumber(e))return s}}return n}function k(t,r){let e=t+(r?Object.entries(r).sort((i,s)=>i[0]<s[0]?-1:1).join():""),n=B.get(e);return n||(n=new Ve(t,r),B.set(e,n)),n}class Ve{parse(r){let e=this.sanitize(r);if(this.symbols.group&&(e=R(e,this.symbols.group,"")),this.symbols.decimal&&(e=e.replace(this.symbols.decimal,".")),this.symbols.minusSign&&(e=e.replace(this.symbols.minusSign,"-")),e=e.replace(this.symbols.numeral,this.symbols.index),this.options.style==="percent"){let u=e.indexOf("-");e=e.replace("-","");let l=e.indexOf(".");l===-1&&(l=e.length),e=e.replace(".",""),l-2===0?e=`0.${e}`:l-2===-1?e=`0.0${e}`:l-2===-2?e="0.00":e=`${e.slice(0,l-2)}.${e.slice(l-2)}`,u>-1&&(e=`-${e}`)}let n=e?+e:NaN;if(isNaN(n))return NaN;if(this.options.style==="percent"){var i,s;let u={...this.options,style:"decimal",minimumFractionDigits:Math.min(((i=this.options.minimumFractionDigits)!==null&&i!==void 0?i:0)+2,20),maximumFractionDigits:Math.min(((s=this.options.maximumFractionDigits)!==null&&s!==void 0?s:0)+2,20)};return new ee(this.locale,u).parse(new De(this.locale,u).format(n))}return this.options.currencySign==="accounting"&&Oe.test(r)&&(n=-1*n),n}sanitize(r){return r=r.replace(this.symbols.literals,""),this.symbols.minusSign&&(r=r.replace("-",this.symbols.minusSign)),this.options.numberingSystem==="arab"&&(this.symbols.decimal&&(r=r.replace(",",this.symbols.decimal),r=r.replace("،",this.symbols.decimal)),this.symbols.group&&(r=R(r,".",this.symbols.group))),this.options.locale==="fr-FR"&&(r=R(r,"."," ")),r}isValidPartialNumber(r,e=-1/0,n=1/0){return r=this.sanitize(r),this.symbols.minusSign&&r.startsWith(this.symbols.minusSign)&&e<0?r=r.slice(this.symbols.minusSign.length):this.symbols.plusSign&&r.startsWith(this.symbols.plusSign)&&n>0&&(r=r.slice(this.symbols.plusSign.length)),this.symbols.group&&r.startsWith(this.symbols.group)||this.symbols.decimal&&r.indexOf(this.symbols.decimal)>-1&&this.options.maximumFractionDigits===0?!1:(this.symbols.group&&(r=R(r,this.symbols.group,"")),r=r.replace(this.symbols.numeral,""),this.symbols.decimal&&(r=r.replace(this.symbols.decimal,"")),r.length===0)}constructor(r,e={}){this.locale=r,this.formatter=new Intl.NumberFormat(r,e),this.options=this.formatter.resolvedOptions(),this.symbols=xe(r,this.formatter,this.options,e);var n,i;this.options.style==="percent"&&(((n=this.options.minimumFractionDigits)!==null&&n!==void 0?n:0)>18||((i=this.options.maximumFractionDigits)!==null&&i!==void 0?i:0)>18)&&console.warn("NumberParser cannot handle percentages with greater than 18 decimal places, please reduce the number in your options.")}}const W=new Set(["decimal","fraction","integer","minusSign","plusSign","group"]),Me=[0,4,2,1,11,20,3,7,100,21,.1,1.1];function xe(t,r,e,n){var i,s,u,l;let m=new Intl.NumberFormat(t,{...e,minimumSignificantDigits:1,maximumSignificantDigits:21,roundingIncrement:1,roundingPriority:"auto",roundingMode:"halfExpand"}),f=m.formatToParts(-10000.111),I=m.formatToParts(10000.111),a=Me.map(c=>m.formatToParts(c));var p;let T=(p=(i=f.find(c=>c.type==="minusSign"))===null||i===void 0?void 0:i.value)!==null&&p!==void 0?p:"-",b=(s=I.find(c=>c.type==="plusSign"))===null||s===void 0?void 0:s.value;!b&&(n?.signDisplay==="exceptZero"||n?.signDisplay==="always")&&(b="+");let x=(u=new Intl.NumberFormat(t,{...e,minimumFractionDigits:2,maximumFractionDigits:2}).formatToParts(.001).find(c=>c.type==="decimal"))===null||u===void 0?void 0:u.value,re=(l=f.find(c=>c.type==="group"))===null||l===void 0?void 0:l.value,ne=f.filter(c=>!W.has(c.type)).map(c=>j(c.value)),ie=a.flatMap(c=>c.filter(y=>!W.has(y.type)).map(y=>j(y.value))),L=[...new Set([...ne,...ie])].sort((c,y)=>y.length-c.length),se=L.length===0?new RegExp("[\\p{White_Space}]","gu"):new RegExp(`${L.join("|")}|[\\p{White_Space}]`,"gu"),U=[...new Intl.NumberFormat(e.locale,{useGrouping:!1}).format(9876543210)].reverse(),ae=new Map(U.map((c,y)=>[c,y])),oe=new RegExp(`[${U.join("")}]`,"g");return{minusSign:T,plusSign:b,decimal:x,group:re,literals:se,numeral:oe,index:c=>String(ae.get(c))}}function R(t,r,e){return t.replaceAll?t.replaceAll(r,e):t.split(r).join(e)}function j(t){return t.replace(/[.*+?^${}()|[\]\\]/g,"\\$&")}var te=le("numberInput").parts("root","label","input","control","valueText","incrementTrigger","decrementTrigger","scrubber"),h=te.build(),o=ue({getRootId:t=>t.ids?.root??`number-input:${t.id}`,getInputId:t=>t.ids?.input??`number-input:${t.id}:input`,getIncrementTriggerId:t=>t.ids?.incrementTrigger??`number-input:${t.id}:inc`,getDecrementTriggerId:t=>t.ids?.decrementTrigger??`number-input:${t.id}:dec`,getScrubberId:t=>t.ids?.scrubber??`number-input:${t.id}:scrubber`,getCursorId:t=>`number-input:${t.id}:cursor`,getLabelId:t=>t.ids?.label??`number-input:${t.id}:label`,getInputEl:t=>o.getById(t,o.getInputId(t)),getIncrementTriggerEl:t=>o.getById(t,o.getIncrementTriggerId(t)),getDecrementTriggerEl:t=>o.getById(t,o.getDecrementTriggerId(t)),getScrubberEl:t=>o.getById(t,o.getScrubberId(t)),getCursorEl:t=>o.getDoc(t).getElementById(o.getCursorId(t)),getPressedTriggerEl:(t,r=t.hint)=>{let e=null;return r==="increment"&&(e=o.getIncrementTriggerEl(t)),r==="decrement"&&(e=o.getDecrementTriggerEl(t)),e},setupVirtualCursor(t){if(!X())return o.createVirtualCursor(t),()=>{o.getCursorEl(t)?.remove()}},preventTextSelection(t){const r=o.getDoc(t),e=r.documentElement,n=r.body;return n.style.pointerEvents="none",e.style.userSelect="none",e.style.cursor="ew-resize",()=>{n.style.pointerEvents="",e.style.userSelect="",e.style.cursor="",e.style.length||e.removeAttribute("style"),n.style.length||n.removeAttribute("style")}},getMousementValue(t,r){const e=P(r.movementX),n=P(r.movementY);let i=e>0?"increment":e<0?"decrement":null;t.isRtl&&i==="increment"&&(i="decrement"),t.isRtl&&i==="decrement"&&(i="increment");const s={x:t.scrubberCursorPoint.x+e,y:t.scrubberCursorPoint.y+n},l=o.getWin(t).innerWidth,m=P(7.5);return s.x=Ie(s.x+m,l)-m,{hint:i,point:s}},createVirtualCursor(t){const r=o.getDoc(t),e=r.createElement("div");e.className="scrubber--cursor",e.id=o.getCursorId(t),Object.assign(e.style,{width:"15px",height:"15px",position:"fixed",pointerEvents:"none",left:"0px",top:"0px",zIndex:pe,transform:t.scrubberCursorPoint?`translate3d(${t.scrubberCursorPoint.x}px, ${t.scrubberCursorPoint.y}px, 0px)`:void 0,willChange:"transform"}),e.innerHTML=`
        <svg width="46" height="15" style="left: -15.5px; position: absolute; top: 0; filter: drop-shadow(rgba(0, 0, 0, 0.4) 0px 1px 1.1px);">
          <g transform="translate(2 3)">
            <path fill-rule="evenodd" d="M 15 4.5L 15 2L 11.5 5.5L 15 9L 15 6.5L 31 6.5L 31 9L 34.5 5.5L 31 2L 31 4.5Z" style="stroke-width: 2px; stroke: white;"></path>
            <path fill-rule="evenodd" d="M 15 4.5L 15 2L 11.5 5.5L 15 9L 15 6.5L 31 6.5L 31 9L 34.5 5.5L 31 2L 31 4.5Z"></path>
          </g>
        </svg>`,r.body.appendChild(e)}});function Le(t,r,e){const n=t.hasTag("focus"),i=t.context.isDisabled,s=t.context.readOnly,u=t.context.isValueEmpty,l=t.context.isOutOfRange||!!t.context.invalid,m=i||!t.context.canIncrement||s,f=i||!t.context.canDecrement||s,I=t.context.translations;return{focused:n,invalid:l,empty:u,value:t.context.formattedValue,valueAsNumber:t.context.valueAsNumber,setValue(a){r({type:"VALUE.SET",value:a})},clearValue(){r("VALUE.CLEAR")},increment(){r("VALUE.INCREMENT")},decrement(){r("VALUE.DECREMENT")},setToMax(){r({type:"VALUE.SET",value:t.context.max})},setToMin(){r({type:"VALUE.SET",value:t.context.min})},focus(){o.getInputEl(t.context)?.focus()},getRootProps(){return e.element({id:o.getRootId(t.context),...h.root.attrs,dir:t.context.dir,"data-disabled":d(i),"data-focus":d(n),"data-invalid":d(l)})},getLabelProps(){return e.label({...h.label.attrs,dir:t.context.dir,"data-disabled":d(i),"data-focus":d(n),"data-invalid":d(l),id:o.getLabelId(t.context),htmlFor:o.getInputId(t.context)})},getControlProps(){return e.element({...h.control.attrs,dir:t.context.dir,role:"group","aria-disabled":i,"data-focus":d(n),"data-disabled":d(i),"data-invalid":d(l),"aria-invalid":F(t.context.invalid)})},getValueTextProps(){return e.element({...h.valueText.attrs,dir:t.context.dir,"data-disabled":d(i),"data-invalid":d(l),"data-focus":d(n)})},getInputProps(){return e.input({...h.input.attrs,dir:t.context.dir,name:t.context.name,form:t.context.form,id:o.getInputId(t.context),role:"spinbutton",defaultValue:t.context.formattedValue,pattern:t.context.pattern,inputMode:t.context.inputMode,"aria-invalid":F(l),"data-invalid":d(l),disabled:i,"data-disabled":d(i),readOnly:t.context.readOnly,required:t.context.required,autoComplete:"off",autoCorrect:"off",spellCheck:"false",type:"text","aria-roledescription":"numberfield","aria-valuemin":t.context.min,"aria-valuemax":t.context.max,"aria-valuenow":Number.isNaN(t.context.valueAsNumber)?void 0:t.context.valueAsNumber,"aria-valuetext":t.context.valueText,onFocus(){r("INPUT.FOCUS")},onBlur(){r("INPUT.BLUR")},onChange(a){r({type:"INPUT.CHANGE",target:a.currentTarget,hint:"set"})},onBeforeInput(a){try{const{selectionStart:p,selectionEnd:T,value:b}=a.currentTarget,M=b.slice(0,p)+(a.data??"")+b.slice(T);t.context.parser.isValidPartialNumber(M)||a.preventDefault()}catch{}},onKeyDown(a){if(a.defaultPrevented||s||fe(a))return;const p=ge(a)*t.context.step,b={ArrowUp(){r({type:"INPUT.ARROW_UP",step:p}),a.preventDefault()},ArrowDown(){r({type:"INPUT.ARROW_DOWN",step:p}),a.preventDefault()},Home(){H(a)||(r("INPUT.HOME"),a.preventDefault())},End(){H(a)||(r("INPUT.END"),a.preventDefault())},Enter(){r("INPUT.ENTER")}}[a.key];b?.(a)}})},getDecrementTriggerProps(){return e.button({...h.decrementTrigger.attrs,dir:t.context.dir,id:o.getDecrementTriggerId(t.context),disabled:f,"data-disabled":d(f),"aria-label":I.decrementLabel,type:"button",tabIndex:-1,"aria-controls":o.getInputId(t.context),onPointerDown(a){f||!_(a)||(r({type:"TRIGGER.PRESS_DOWN",hint:"decrement",pointerType:a.pointerType}),a.pointerType==="mouse"&&a.preventDefault(),a.pointerType==="touch"&&a.currentTarget?.focus({preventScroll:!0}))},onPointerUp(a){r({type:"TRIGGER.PRESS_UP",hint:"decrement",pointerType:a.pointerType})},onPointerLeave(){f||r({type:"TRIGGER.PRESS_UP",hint:"decrement"})}})},getIncrementTriggerProps(){return e.button({...h.incrementTrigger.attrs,dir:t.context.dir,id:o.getIncrementTriggerId(t.context),disabled:m,"data-disabled":d(m),"aria-label":I.incrementLabel,type:"button",tabIndex:-1,"aria-controls":o.getInputId(t.context),onPointerDown(a){m||!_(a)||(r({type:"TRIGGER.PRESS_DOWN",hint:"increment",pointerType:a.pointerType}),a.pointerType==="mouse"&&a.preventDefault(),a.pointerType==="touch"&&a.currentTarget?.focus({preventScroll:!0}))},onPointerUp(a){r({type:"TRIGGER.PRESS_UP",hint:"increment",pointerType:a.pointerType})},onPointerLeave(a){r({type:"TRIGGER.PRESS_UP",hint:"increment",pointerType:a.pointerType})}})},getScrubberProps(){return e.element({...h.scrubber.attrs,dir:t.context.dir,"data-disabled":d(i),id:o.getScrubberId(t.context),role:"presentation",onMouseDown(a){if(i)return;const p=be(a);p.x=p.x-P(7.5),p.y=p.y-P(7.5),r({type:"SCRUBBER.PRESS_DOWN",point:p}),a.preventDefault()},style:{cursor:i?void 0:"ew-resize"}})}}}function Ue(t){if(t.ownerDocument.activeElement===t)try{const{selectionStart:r,selectionEnd:e,value:n}=t,i=n.substring(0,r),s=n.substring(e);return{start:r,end:e,value:n,beforeTxt:i,afterTxt:s}}catch{}}function Fe(t,r){if(t.ownerDocument.activeElement===t){if(!r){t.setSelectionRange(t.value.length,t.value.length);return}try{const{value:e}=t,{beforeTxt:n="",afterTxt:i="",start:s}=r;let u=e.length;if(e.endsWith(i))u=e.length-i.length;else if(e.startsWith(n))u=n.length;else if(s!=null){const l=n[s-1],m=e.indexOf(l,s-1);m!==-1&&(u=m+1)}t.setSelectionRange(u,u)}catch{}}}var Z=(t,r={})=>q(new Intl.NumberFormat(t,r)),Y=(t,r={})=>q(new ee(t,r)),K=(t,r)=>t.formatOptions?t.parser.parse(String(r)):parseFloat(r),v=(t,r)=>Number.isNaN(r)?"":t.formatOptions?t.formatter.format(r):r.toString(),{not:O,and:S}=he;function _e(t){const r=ce(t);return me({id:"number-input",initial:"idle",context:{dir:"ltr",locale:"en-US",focusInputOnChange:!0,clampValueOnBlur:!0,allowOverflow:!1,inputMode:"decimal",pattern:"[0-9]*(.[0-9]+)?",value:"",step:1,min:Number.MIN_SAFE_INTEGER,max:Number.MAX_SAFE_INTEGER,invalid:!1,spinOnPress:!0,disabled:!1,readOnly:!1,...r,hint:null,scrubberCursorPoint:null,fieldsetDisabled:!1,formatter:Z(r.locale||"en-US",r.formatOptions),parser:Y(r.locale||"en-US",r.formatOptions),translations:{incrementLabel:"increment value",decrementLabel:"decrease value",...r.translations}},computed:{isRtl:e=>e.dir==="rtl",valueAsNumber:e=>K(e,e.value),formattedValue:e=>v(e,e.valueAsNumber),isAtMin:e=>Se(e.valueAsNumber,e),isAtMax:e=>Re(e.valueAsNumber,e),isOutOfRange:e=>!Ce(e.valueAsNumber,e),isValueEmpty:e=>e.value==="",isDisabled:e=>!!e.disabled||e.fieldsetDisabled,canIncrement:e=>e.allowOverflow||!e.isAtMax,canDecrement:e=>e.allowOverflow||!e.isAtMin,valueText:e=>e.translations.valueText?.(e.value)},watch:{formatOptions:["setFormatterAndParser","syncInputElement"],locale:["setFormatterAndParser","syncInputElement"],value:["syncInputElement"],isOutOfRange:["invokeOnInvalid"],scrubberCursorPoint:["setVirtualCursorPosition"]},activities:["trackFormControl"],on:{"VALUE.SET":{actions:["setRawValue","setHintToSet"]},"VALUE.CLEAR":{actions:["clearValue"]},"VALUE.INCREMENT":{actions:["increment"]},"VALUE.DECREMENT":{actions:["decrement"]}},states:{idle:{on:{"TRIGGER.PRESS_DOWN":[{guard:"isTouchPointer",target:"before:spin",actions:["setHint"]},{target:"before:spin",actions:["focusInput","invokeOnFocus","setHint"]}],"SCRUBBER.PRESS_DOWN":{target:"scrubbing",actions:["focusInput","invokeOnFocus","setHint","setCursorPoint"]},"INPUT.FOCUS":{target:"focused",actions:["focusInput","invokeOnFocus"]}}},focused:{tags:"focus",activities:"attachWheelListener",on:{"TRIGGER.PRESS_DOWN":[{guard:"isTouchPointer",target:"before:spin",actions:["setHint"]},{target:"before:spin",actions:["focusInput","setHint"]}],"SCRUBBER.PRESS_DOWN":{target:"scrubbing",actions:["focusInput","setHint","setCursorPoint"]},"INPUT.ARROW_UP":{actions:"increment"},"INPUT.ARROW_DOWN":{actions:"decrement"},"INPUT.HOME":{actions:"decrementToMin"},"INPUT.END":{actions:"incrementToMax"},"INPUT.CHANGE":{actions:["setValue","setHint"]},"INPUT.BLUR":[{guard:S("clampValueOnBlur",O("isInRange")),target:"idle",actions:["setClampedValue","clearHint","invokeOnBlur"]},{target:"idle",actions:["setFormattedValue","clearHint","invokeOnBlur"]}],"INPUT.ENTER":{actions:["setFormattedValue","clearHint","invokeOnBlur"]}}},"before:spin":{tags:"focus",activities:"trackButtonDisabled",entry:de([{guard:"isIncrementHint",actions:"increment"},{guard:"isDecrementHint",actions:"decrement"}]),after:{CHANGE_DELAY:{target:"spinning",guard:S("isInRange","spinOnPress")}},on:{"TRIGGER.PRESS_UP":[{guard:"isTouchPointer",target:"focused",actions:"clearHint"},{target:"focused",actions:["focusInput","clearHint"]}]}},spinning:{tags:"focus",activities:"trackButtonDisabled",every:[{delay:"CHANGE_INTERVAL",guard:S(O("isAtMin"),"isIncrementHint"),actions:"increment"},{delay:"CHANGE_INTERVAL",guard:S(O("isAtMax"),"isDecrementHint"),actions:"decrement"}],on:{"TRIGGER.PRESS_UP":{target:"focused",actions:["focusInput","clearHint"]}}},scrubbing:{tags:"focus",activities:["activatePointerLock","trackMousemove","setupVirtualCursor","preventTextSelection"],on:{"SCRUBBER.POINTER_UP":{target:"focused",actions:["focusInput","clearCursorPoint"]},"SCRUBBER.POINTER_MOVE":[{guard:"isIncrementHint",actions:["increment","setCursorPoint"]},{guard:"isDecrementHint",actions:["decrement","setCursorPoint"]}]}}}},{delays:{CHANGE_INTERVAL:50,CHANGE_DELAY:300},guards:{clampValueOnBlur:e=>e.clampValueOnBlur,isAtMin:e=>e.isAtMin,spinOnPress:e=>!!e.spinOnPress,isAtMax:e=>e.isAtMax,isInRange:e=>!e.isOutOfRange,isDecrementHint:(e,n)=>(n.hint??e.hint)==="decrement",isIncrementHint:(e,n)=>(n.hint??e.hint)==="increment",isTouchPointer:(e,n)=>n.pointerType==="touch"},activities:{trackFormControl(e,n,{initialContext:i}){const s=o.getInputEl(e);return Pe(s,{onFieldsetDisabledChange(u){e.fieldsetDisabled=u},onFormReset(){g.value(e,i.value)}})},setupVirtualCursor(e){return o.setupVirtualCursor(e)},preventTextSelection(e){return o.preventTextSelection(e)},trackButtonDisabled(e,n,{send:i}){const s=o.getPressedTriggerEl(e,e.hint);return ye(s,{attributes:["disabled"],callback(){i({type:"TRIGGER.PRESS_UP",src:"attr"})}})},attachWheelListener(e,n,{send:i}){const s=o.getInputEl(e);if(!s||!o.isActiveElement(e,s)||!e.allowMouseWheel)return;function u(l){l.preventDefault();const m=Math.sign(l.deltaY)*-1;m===1?i("VALUE.INCREMENT"):m===-1&&i("VALUE.DECREMENT")}return D(s,"wheel",u,{passive:!1})},activatePointerLock(e){if(!X())return ve(o.getDoc(e))},trackMousemove(e,n,{send:i}){const s=o.getDoc(e);function u(m){if(!e.scrubberCursorPoint)return;const f=o.getMousementValue(e,m);f.hint&&i({type:"SCRUBBER.POINTER_MOVE",hint:f.hint,point:f.point})}function l(){i("SCRUBBER.POINTER_UP")}return Ee(D(s,"mousemove",u,!1),D(s,"mouseup",l,!1))}},actions:{focusInput(e){if(!e.focusInputOnChange)return;const n=o.getInputEl(e);o.isActiveElement(e,n)||z(()=>n?.focus({preventScroll:!0}))},increment(e,n){const i=Te(e.valueAsNumber,n.step??e.step),s=v(e,N(i,e));g.value(e,s)},decrement(e,n){const i=Ne(e.valueAsNumber,n.step??e.step),s=v(e,N(i,e));g.value(e,s)},setClampedValue(e){const n=N(e.valueAsNumber,e);g.value(e,v(e,n))},setRawValue(e,n){const i=K(e,n.value),s=v(e,N(i,e));g.value(e,s)},setValue(e,n){const i=n.target?.value??n.value;g.value(e,i)},clearValue(e){g.value(e,"")},incrementToMax(e){const n=v(e,e.max);g.value(e,n)},decrementToMin(e){const n=v(e,e.min);g.value(e,n)},setHint(e,n){e.hint=n.hint},clearHint(e){e.hint=null},setHintToSet(e){e.hint="set"},invokeOnFocus(e){e.onFocusChange?.({focused:!0,value:e.formattedValue,valueAsNumber:e.valueAsNumber})},invokeOnBlur(e){e.onFocusChange?.({focused:!1,value:e.formattedValue,valueAsNumber:e.valueAsNumber})},invokeOnInvalid(e){if(!e.isOutOfRange)return;const n=e.valueAsNumber>e.max?"rangeOverflow":"rangeUnderflow";e.onValueInvalid?.({reason:n,value:e.formattedValue,valueAsNumber:e.valueAsNumber})},syncInputElement(e,n){const i=n.type.endsWith("CHANGE")?e.value:e.formattedValue;He.input(e,i)},setFormattedValue(e){g.value(e,e.formattedValue)},setCursorPoint(e,n){e.scrubberCursorPoint=n.point},clearCursorPoint(e){e.scrubberCursorPoint=null},setVirtualCursorPosition(e){const n=o.getCursorEl(e);if(!n||!e.scrubberCursorPoint)return;const{x:i,y:s}=e.scrubberCursorPoint;n.style.transform=`translate3d(${i}px, ${s}px, 0px)`},setFormatterAndParser(e){e.locale&&(e.formatter=Z(e.locale,e.formatOptions),e.parser=Y(e.locale,e.formatOptions))}},compareFns:{formatOptions:(e,n)=>w(e,n),scrubberCursorPoint:(e,n)=>w(e,n)}})}var He={input(t,r){const e=o.getInputEl(t);if(!e)return;const n=Ue(e);z(()=>{o.setValue(e,r),Fe(e,n)})}},Ge={onChange:t=>{t.onValueChange?.({value:t.value,valueAsNumber:t.valueAsNumber})}},g={value:(t,r)=>{w(t.value,r)||(t.value=r,Ge.onChange(t))}};const je=Object.freeze(Object.defineProperty({__proto__:null,anatomy:te,connect:Le,machine:_e},Symbol.toStringTag,{value:"Module"}));export{Le as c,_e as m,je as n};
